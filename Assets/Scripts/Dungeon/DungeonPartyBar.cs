using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Abyssdawn;   // MonsterSO

/// <summary>
/// 던전 화면 아래의 파티 상태 카드 (위저드리 다프네식). 전투 씬의 PartyBar 카드를 그대로 가져다 놓으면 자동 연결된다.
///  - 카드 찾기: partyBar 칸이 비어 있으면 이름 "PartyBar" 로 찾음 → 그 아래 PartySlot_1~4 (전투와 같은 이름 규칙).
///  - 순서: 전투와 동일 — Hero 는 CompanionPartyPersistence.heroSlotIndex 칸, 나머지 칸은 ActiveRoster 순서.
///  - 주인공: 씬의 PlayerStats 실시간 (HP/MP, 상태이상 아이콘). 함정·샘·레벨업 즉시 반영 (PlayerStats.OnStatusChanged).
///  - 동료: ActiveRoster 의 현재 HP/MP + MonsterSO 의 최대치·이름. 던전에서는 동료 상태이상 기록이 없어 아이콘은 비움.
///  - 쓰러진 캐릭터: 글자 붉게 + 해골 그림 (전투와 동일, PartyCardVisuals).
/// 배치(위치·크기)는 씬에서 직접 — 이 스크립트는 값만 채운다. MapManager 가 실행 시 자동으로 붙인다.
/// </summary>
public class DungeonPartyBar : MonoBehaviour
{
    [Tooltip("파티 카드 묶음 (PartySlot_1~4 의 부모). 비워 두면 이름 'PartyBar' 로 찾는다")]
    public Transform partyBar;

    [Tooltip("동료 교체·순서 변경 등 이벤트가 없는 변화를 반영하는 주기 (초)")]
    public float refreshInterval = 0.25f;

    private static readonly Color AliveTextColor = Color.white;
    private static readonly Color DownedTextColor = new Color(1f, 0.5f, 0.5f, 1f); // 전투와 같은 기절 색

    private class Card
    {
        public Transform slot;
        public Transform skullParent;
        public TextMeshProUGUI nameText, hpText, mpText, levelText;
        public List<Image> icons;
    }

    private readonly List<Card> _cards = new List<Card>();
    private PlayerStats _hero;
    private float _nextRefresh;
    private float _nextBindTry;
    private bool _warned;
    private int _lastSignature;

    private void OnEnable()
    {
        PlayerStats.OnStatusChanged += Refresh;
        _nextRefresh = 0f;
    }

    private void OnDisable()
    {
        PlayerStats.OnStatusChanged -= Refresh;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        // 바뀐 게 있을 때만 다시 채운다 (주인공 최대 HP 계산은 로그를 많이 남겨서 매번 부르지 않음)
        int sig = PartySignature();
        if (sig == _lastSignature && _cards.Count > 0) return;
        Refresh();
    }

    /// <summary>파티 구성·현재 HP/MP 요약값. 이벤트 없이 바뀌는 것(동료 교체·순서 변경 등) 감지용.</summary>
    private int PartySignature()
    {
        unchecked
        {
            int h = 17 + CompanionPartyPersistence.heroSlotIndex;
            if (_hero == null) _hero = FindFirstObjectByType<PlayerStats>();
            if (_hero != null) h = h * 31 + _hero.currentHP * 7919 + _hero.currentMP * 131 + _hero.level;
            foreach (var e in CompanionPartyPersistence.ActiveRoster)
                h = h * 31 + (e == null ? -1 : e.id * 7919 + e.currentHP * 131 + e.currentMP);
            return h;
        }
    }

    /// <summary>카드 전체를 현재 파티 상태로 다시 채운다.</summary>
    public void Refresh()
    {
        if (!EnsureBound()) return;
        if (_hero == null) _hero = FindFirstObjectByType<PlayerStats>();
        _lastSignature = PartySignature();

        var occupancy = CompanionPartyPersistence.BuildMainLineOccupancy();
        for (int i = 0; i < _cards.Count; i++)
        {
            Card card = _cards[i];
            if (card == null || card.slot == null) continue;

            if (i >= occupancy.Length || occupancy[i].IsEmpty)
            {
                FillEmpty(card);
            }
            else if (occupancy[i].IsHero)
            {
                if (_hero == null) FillEmpty(card);
                else FillCard(card, _hero.playerName, _hero.level, _hero.currentHP, _hero.maxHP, _hero.currentMP, _hero.maxMP, _hero);
            }
            else
            {
                var entry = occupancy[i].entry;
                MonsterSO so = CompanionPartyPersistence.LoadCompanion(entry.resourcePath);
                if (so == null) { FillEmpty(card); continue; }
                FillCard(card, so.MonsterName, so.MonsterLevel, entry.currentHP, so.HP, entry.currentMP, so.MP, null);
            }
        }
    }

    private void FillCard(Card card, string name, int level, int hp, int maxHp, int mp, int maxMp, PlayerStats statusSource)
    {
        bool dead = hp <= 0;
        Color c = dead ? DownedTextColor : AliveTextColor;
        SetText(card.nameText, name, c);
        SetText(card.levelText, level.ToString(), c);
        SetText(card.hpText, $"HP {hp}/{maxHp}", c);
        SetText(card.mpText, $"MP {mp}/{maxMp}", c);
        PartyCardVisuals.SetDeathSkull(card.skullParent, dead);
        PartyCardVisuals.ShowStatusIcons(card.icons, statusSource);
    }

    private void FillEmpty(Card card)
    {
        SetText(card.nameText, "", AliveTextColor);
        SetText(card.levelText, "", AliveTextColor);
        SetText(card.hpText, "", AliveTextColor);
        SetText(card.mpText, "", AliveTextColor);
        PartyCardVisuals.SetDeathSkull(card.skullParent, false);
        PartyCardVisuals.ShowStatusIcons(card.icons, null);
    }

    private static void SetText(TextMeshProUGUI tmp, string text, Color color)
    {
        if (tmp == null) return;
        if (tmp.text != text) tmp.text = text;
        tmp.color = color;
    }

    /// <summary>PartyBar 와 카드 4장을 찾아 둔다. 아직 씬에 없으면 1초마다 다시 찾는다.</summary>
    private bool EnsureBound()
    {
        if (_cards.Count > 0 && partyBar != null) return true;
        if (Time.unscaledTime < _nextBindTry) return false;
        _nextBindTry = Time.unscaledTime + 1f;

        if (partyBar == null)
        {
            GameObject go = GameObject.Find(PartyCardVisuals.PartyBarName);
            if (go != null) partyBar = go.transform;
        }
        if (partyBar == null)
        {
            if (!_warned)
            {
                Debug.Log($"[DungeonPartyBar] 씬에 '{PartyCardVisuals.PartyBarName}' 이(가) 아직 없어 파티 카드를 표시하지 않습니다 (배치하면 자동 연결).");
                _warned = true;
            }
            return false;
        }

        _cards.Clear();
        for (int i = 0; i < CompanionPartyPersistence.MainLineSlots; i++)
        {
            Transform slot = PartyCardVisuals.FindSlot(partyBar, i);
            if (slot == null) { _cards.Add(null); continue; }

            var card = new Card
            {
                slot = slot,
                nameText = PartyCardVisuals.FindText(slot, "Nametext", "NameText"),
                hpText = PartyCardVisuals.FindText(slot, "HPText"),
                mpText = PartyCardVisuals.FindText(slot, "MPText"),
                levelText = PartyCardVisuals.FindText(slot, PartyCardVisuals.LevelTextNames),
                icons = PartyCardVisuals.FindStatusIcons(slot),
            };
            // 해골은 전투와 같은 자리 — 글자의 부모(카드 안쪽 배경) 맨 아래 층
            TextMeshProUGUI anchor = card.hpText != null ? card.hpText : (card.nameText != null ? card.nameText : card.mpText);
            card.skullParent = anchor != null ? anchor.transform.parent : slot;
            _cards.Add(card);
        }

        int found = 0;
        foreach (var c in _cards) if (c != null) found++;
        if (found == 0)
        {
            if (!_warned)
            {
                Debug.LogWarning($"[DungeonPartyBar] '{partyBar.name}' 아래에서 PartySlot_1~4 를 찾지 못했습니다.");
                _warned = true;
            }
            _cards.Clear();
            return false;
        }
        // 던전의 카드는 보기 전용 — 투명한 카드 배경이 이동 버튼 등의 터치를 가로채지 않게
        foreach (Graphic g in partyBar.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        Debug.Log($"[DungeonPartyBar] '{partyBar.name}' 연결 — 카드 {found}/{CompanionPartyPersistence.MainLineSlots}장");
        return true;
    }
}
