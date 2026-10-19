using System.Collections;
using System.Collections.Generic;
using AbyssdawnBattle;
using UnityEngine;

/// <summary>
/// [2026-10-17] 전투 연출 3종 (레트로 규칙: 사각 픽셀 조각, 둥근·부드러운 모양 없음)
///  1) 공격 동작 — 적은 공격 직전 화면 쪽으로 튀어나왔다 돌아감, 아군은 카드가 위로 살짝 튐
///  2) 무기별 베기 궤적 — 맞는 순간 몬스터 위에 픽셀 조각으로 그린다
///     검: 대각선 베기 · 도끼: 굵은 내려찍기 + 파편 · 단검: X 두 번 · 창/폴암/활: 가로 찌르기 · 맨손/망치: 사각 충격파 · 마법: 파란 사각 고리
///  3) 처치 — 몬스터 그림을 사각 조각으로 잘라 흩어지며 떨어진다
/// BattleManager 가 BeginAllyAction / EnemyLunge 를 부르고, EnemyStats 가 OnEnemyHit / Crumble 을 부른다.
/// </summary>
public class BattleFxCombat : MonoBehaviour
{
    private static BattleFxCombat _inst;
    private static BattleFxCombat Get()
    {
        if (_inst != null) return _inst;
        if (Object.FindFirstObjectByType<global::BattleManager>() == null) return null; // 전투 씬에서만
        var go = new GameObject("BattleFxCombat");
        _inst = go.AddComponent<BattleFxCombat>();
        return _inst;
    }

    private void OnDestroy() { if (_inst == this) _inst = null; }

    // ── 지금 행동 중인 아군 (베기 모양을 고르려고) ──
    private static PlayerStats _attacker;
    private static SkillData _skill;
    private static float _attackerSetAt;
    private static int _hitIndex;

    /// <summary>아군 행동 시작 (공격·스킬). 카드가 위로 튄다.</summary>
    public static void BeginAllyAction(PlayerStats actor, SkillData skill, RectTransform card)
    {
        _attacker = actor; _skill = skill; _attackerSetAt = Time.unscaledTime; _hitIndex = 0;
        var fx = Get();
        if (fx != null && card != null) fx.StartCoroutine(fx.Hop(card, 28f));
    }

    /// <summary>아군 행동 끝 / 적 차례 시작 — 이후의 피해(지속 피해 등)에는 베기를 그리지 않는다.</summary>
    public static void EndAllyAction() { _attacker = null; _skill = null; }

    // ─────────────────────────────────────────
    // 1) 공격 동작
    // ─────────────────────────────────────────
    private readonly HashSet<Transform> _moving = new HashSet<Transform>();

    /// <summary>적이 공격 직전 화면 쪽(아래)으로 튀어나왔다 돌아감. 부른 쪽은 약 0.12초 뒤 피해를 넣으면 맞는 순간과 맞는다.</summary>
    public static void EnemyLunge(EnemyStats e)
    {
        var fx = Get();
        if (fx == null || e == null) return;
        fx.StartCoroutine(fx.Lunge(e.transform));
    }

    public const float LungeLead = 0.12f; // 튀어나오는 데 걸리는 시간 (피해를 넣기 전 기다릴 시간)

    private IEnumerator Lunge(Transform t)
    {
        if (t == null || _moving.Contains(t)) yield break;
        _moving.Add(t);
        Vector3 pos0 = t.position, scale0 = t.localScale;
        Vector3 pos1 = pos0 + new Vector3(0f, -0.18f, 0f);
        Vector3 scale1 = scale0 * 1.12f;
        for (float e = 0f; e < LungeLead && t != null; e += Time.deltaTime)
        {
            float k = e / LungeLead; k = k * k; // 점점 빠르게 덤빈다
            t.position = Vector3.Lerp(pos0, pos1, k); t.localScale = Vector3.Lerp(scale0, scale1, k);
            yield return null;
        }
        yield return new WaitForSeconds(0.06f);
        for (float e = 0f; e < 0.2f && t != null; e += Time.deltaTime)
        {
            float k = e / 0.2f; k = 1f - (1f - k) * (1f - k);
            t.position = Vector3.Lerp(pos1, pos0, k); t.localScale = Vector3.Lerp(scale1, scale0, k);
            yield return null;
        }
        if (t != null) { t.position = pos0; t.localScale = scale0; }
        _moving.Remove(t);
    }

    private IEnumerator Hop(RectTransform t, float dist)
    {
        if (t == null || _moving.Contains(t)) yield break;
        _moving.Add(t);
        Vector3 home = t.localPosition, up = home + new Vector3(0f, dist, 0f);
        for (float e = 0f; e < 0.08f && t != null; e += Time.unscaledDeltaTime) { t.localPosition = Vector3.Lerp(home, up, e / 0.08f); yield return null; }
        for (float e = 0f; e < 0.16f && t != null; e += Time.unscaledDeltaTime) { float k = e / 0.16f; t.localPosition = Vector3.Lerp(up, home, 1f - (1f - k) * (1f - k)); yield return null; }
        if (t != null) t.localPosition = home;
        _moving.Remove(t);
    }

    // ─────────────────────────────────────────
    // 2) 무기별 베기 궤적
    // ─────────────────────────────────────────
    private enum SlashKind { Sword, Axe, Dagger, Thrust, Blunt, Magic }

    /// <summary>EnemyStats.TakeDamage 가 부른다 (일반 피해만). 지금 아군이 행동 중일 때만 그린다.</summary>
    public static void OnEnemyHit(EnemyStats e, bool crit)
    {
        if (e == null || _attacker == null || Time.unscaledTime - _attackerSetAt > 4f) return;
        var fx = Get();
        if (fx == null) return;
        var sr = e.GetComponent<SpriteRenderer>();
        if (sr == null) sr = e.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;
        // [2026-10-17] 지금은 "검 일반 공격" 만 — 스킬·다른 무기 이펙트는 모두 끔 (하나씩 다시 열 예정)
        if (_skill != null && _skill.Key != "Slash") return; // [2026-10-18] 스킬 "베기"도 같은 효과
        SlashKind kind = KindFor(_attacker, _skill, _hitIndex);
        _hitIndex++;
        if (kind != SlashKind.Sword) return;
        // [2026-10-18] 웹 원본 그림(CC0 · Cethiel "Weapon Slash" 4번째 줄, 은색)은 스킬 "베기" 전용. 일반 공격은 예전 픽셀 칼선.
        bool slashSkill = _skill != null && _skill.Key == "Slash";
        if (slashSkill && StabFrames.Length > 0) fx.StartCoroutine(fx.StabFlip(sr, crit));
        else fx.StartCoroutine(fx.Slash(sr, kind, crit));
    }

    // [2026-10-18] 검 일반 공격 — 그림 6장 넘김 (Resources/FX/SlashStab, 1시 → 7시로 찌르듯 베기)
    private static Sprite[] _stab;
    private static Sprite[] StabFrames
    {
        get
        {
            if (_stab == null)
            {
                var list = new List<Sprite>(Resources.LoadAll<Sprite>("FX/SlashStab"));
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                _stab = list.ToArray();
            }
            return _stab;
        }
    }
    private const float StabFrameTime = 0.075f; // [2026-10-18] 더 느리게 (0.045 → 0.075)

    // [2026-10-20] 효과 크기 고정: 몬스터 그림 크기 대신 화면 높이의 일정 비율 (쥐 크기쯤). 어느 적이든 같은 크기
    public const float FxScreenFraction = 0.19f;
    public const float StabScreenFraction = 0.12f; // [2026-10-20] 베기 연출만 더 작게 (0.19 → 0.12)
    private static Bounds FixedBounds(SpriteRenderer target, float fraction = FxScreenFraction)
    {
        Vector3 c = target.bounds.center;
        float size = 2f;
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(c);
            Vector3 up = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y + Screen.height * fraction, sp.z));
            size = Mathf.Max(0.01f, Vector3.Distance(c, up));
        }
        return new Bounds(c, new Vector3(size, size, 0.01f));
    }

    private const float StabAngle = 46f;        // 그림 속 칼날 방향 (오른쪽 위 ↗ 기준 각도)
    private const float StabLengthScale = 1.7f; // [2026-10-18] 칼날 길이만 1.7배

    private IEnumerator StabFlip(SpriteRenderer target, bool crit)
    {
        var frames = StabFrames;
        Bounds b = FixedBounds(target, StabScreenFraction); // [2026-10-20] 몬스터 크기와 상관없이 같은 크기 (쥐 기준)
        var go = new GameObject("SlashStab");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(b.center.x, b.center.y, b.center.z - 0.01f);
        float h = frames[0].bounds.size.y;
        float s = h > 0f ? Mathf.Max(b.size.x, b.size.y) * (crit ? 1.8f : 1.6f) / h : 1f; // [2026-10-18] 적보다 길게 (긴 변의 1.6배)
        // [2026-10-18] 길이만 늘림 (굵기는 그대로): 칼날 방향(그림 속 약 46°)으로만 늘이고, 그림은 안쪽 자식에서 되돌려 세운다
        go.transform.localRotation = Quaternion.Euler(0f, 0f, StabAngle);
        go.transform.localScale = new Vector3(StabLengthScale, 1f, 1f);
        var pic = new GameObject("Frame");
        pic.transform.SetParent(go.transform, false);
        pic.transform.localRotation = Quaternion.Euler(0f, 0f, -StabAngle);
        pic.transform.localScale = new Vector3(s, s, 1f);
        var sr = pic.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = target.sortingLayerID; sr.sortingOrder = target.sortingOrder + 20;
        for (int i = 0; i < frames.Length; i++)
        {
            if (sr == null) yield break;
            sr.sprite = frames[i];
            yield return new WaitForSecondsRealtime(StabFrameTime);
        }
        if (go != null) Destroy(go);
    }

    private static SlashKind KindFor(PlayerStats a, SkillData skill, int hit)
    {
        if (skill != null && skill.damageType == DamageType.Magic) return SlashKind.Magic;
        EquipmentData r = null, l = null;
        var eq = a.GetComponent<EquipmentManager>();
        if (eq != null) { r = eq.rightHand; l = eq.leftHand; }
        else if (a.statData != null) { r = a.statData.rightHand; l = a.statData.leftHand; }
        EquipmentData w = (hit % 2 == 1 && l != null && l.weaponCategory != WeaponCategory.None && l.weaponCategory != WeaponCategory.Shield) ? l : r; // 쌍수면 번갈아
        if (w == null) return SlashKind.Blunt;
        switch (w.weaponCategory)
        {
            case WeaponCategory.Sword: case WeaponCategory.Katana: return SlashKind.Sword;
            case WeaponCategory.Axe: return SlashKind.Axe;
            case WeaponCategory.Dagger: return SlashKind.Dagger;
            case WeaponCategory.Spear: case WeaponCategory.Polearm: case WeaponCategory.Bow: return SlashKind.Thrust;
            default: return SlashKind.Blunt;
        }
    }

    private static Sprite _px;
    private static Sprite Px
    {
        get
        {
            if (_px == null) _px = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _px;
        }
    }

    private static readonly Color Core = new Color(1f, 1f, 1f, 1f);              // [2026-10-17] 드퀘 일반 공격처럼 흰색·은회색
    private static readonly Color Edge = new Color(0.72f, 0.75f, 0.80f, 1f);
    private static readonly Color CritEdge = new Color(0.90f, 0.92f, 0.98f, 1f);
    private static readonly Color Wind = new Color(0.85f, 0.88f, 0.93f, 0.35f);   // 바람 가르는 흐린 자국
    private static readonly Color MagicCol = new Color(0.5f, 0.75f, 1f, 1f);

    private IEnumerator Slash(SpriteRenderer target, SlashKind kind, bool crit)
    {
        Bounds b = FixedBounds(target); // [2026-10-20] 몬스터 크기와 상관없이 같은 크기 (쥐 기준)
        bool sharp = kind == SlashKind.Sword || kind == SlashKind.Thrust; // [2026-10-17] 베기·찌르기는 더 가늘고 길게
        float q = sharp ? Mathf.Max(0.02f, b.size.x / 26f) : Mathf.Max(0.035f, b.size.x / 16f);  // 픽셀 조각 한 칸 크기
        Vector3 c = b.center;
        float ex = b.extents.x * (sharp ? 1.25f : 0.85f), ey = b.extents.y * (sharp ? 1.05f : 0.75f);
        Color edge = crit ? CritEdge : Edge;
        var cells = new List<KeyValuePair<Vector2, Color>>(); // (정규화 좌표 -1~1, 색) — 그리는 순서대로
        switch (kind)
        {
            case SlashKind.Sword: Blade(cells, new Vector2(0.55f, 0.95f), new Vector2(-0.55f, -0.95f), 30, edge, q / ex); break; // [2026-10-17] 1시 → 7시, 가운데만 두껍고 양끝이 가늘게
            case SlashKind.Axe:
                Line(cells, new Vector2(-0.05f, 1f), new Vector2(0.05f, -0.6f), 11, Core, edge, true);
                Line(cells, new Vector2(0.12f, 1f), new Vector2(0.18f, -0.5f), 9, edge, edge, false);
                for (int i = 0; i < 6; i++) cells.Add(new KeyValuePair<Vector2, Color>(new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.85f, -0.55f)), edge));
                break;
            case SlashKind.Dagger:
                Line(cells, new Vector2(-0.5f, 0.5f), new Vector2(0.5f, -0.5f), 7, Core, edge, false);
                Line(cells, new Vector2(0.5f, 0.5f), new Vector2(-0.5f, -0.5f), 7, Core, edge, false);
                break;
            case SlashKind.Thrust: Blade(cells, new Vector2(-1f, 0.06f), new Vector2(1f, -0.06f), 28, edge, q / ey); break;
            case SlashKind.Magic: Ring(cells, 0.55f, MagicCol); Ring(cells, 0.85f, new Color(MagicCol.r, MagicCol.g, MagicCol.b, 0.7f)); break;
            default: Ring(cells, 0.35f, Core); Ring(cells, 0.6f, edge); break;
        }

        var made = new List<SpriteRenderer>();
        float step = sharp ? 0.002f : kind == SlashKind.Magic || kind == SlashKind.Blunt ? 0.004f : 0.01f;
        int perStep = sharp ? 6 : 2; // 베기는 거의 한 번에 그어진다
        for (int i = 0; i < cells.Count; i++)
        {
            if (target == null) break;
            var p = cells[i];
            var go = new GameObject("SlashPx");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(Snap(c.x + p.Key.x * ex, q), Snap(c.y + p.Key.y * ey, q), c.z - 0.01f);
            go.transform.localScale = new Vector3(q, q, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Px; sr.color = p.Value;
            sr.sortingLayerID = target.sortingLayerID; sr.sortingOrder = target.sortingOrder + 20;
            made.Add(sr);
            if (step > 0f && i % perStep == perStep - 1) yield return new WaitForSecondsRealtime(step);
        }
        yield return new WaitForSecondsRealtime(sharp ? 0.05f : 0.09f);
        // 계단식으로 사라짐 (부드럽게 녹지 않게 3단계)
        float[] alphas = { 0.6f, 0.3f, 0f };
        foreach (float a in alphas)
        {
            foreach (var sr in made) if (sr != null) { var col = sr.color; col.a = Mathf.Min(col.a, a); sr.color = col; }
            yield return new WaitForSecondsRealtime(sharp ? 0.03f : 0.05f);
        }
        foreach (var sr in made) if (sr != null) Destroy(sr.gameObject);
    }

    private static float Snap(float v, float q) { return Mathf.Round(v / q) * q; }

    private static void Line(List<KeyValuePair<Vector2, Color>> cells, Vector2 a, Vector2 b, int n, Color core, Color edge, bool thick)
    {
        Vector2 perp = new Vector2(-(b - a).y, (b - a).x).normalized * 0.09f;
        for (int i = 0; i <= n; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)n);
            cells.Add(new KeyValuePair<Vector2, Color>(p, core));
            if (thick && i > 1 && i < n - 1) cells.Add(new KeyValuePair<Vector2, Color>(p + perp, edge));
            else if (i % 2 == 0) cells.Add(new KeyValuePair<Vector2, Color>(p - perp, edge));
        }
    }

    /// <summary>[2026-10-17] 날카로운 칼날 궤적: 양끝 1칸 → 가운데 3칸 두께, 가운데 줄은 흰색, 바깥 줄은 금색. a 에서 b 로 그어진다.</summary>
    private static void Blade(List<KeyValuePair<Vector2, Color>> cells, Vector2 a, Vector2 b, int n, Color edge, float cell)
    {
        Vector2 dir = (b - a);
        Vector2 perp = new Vector2(-dir.y, dir.x).normalized * cell; // 한 칸 옆
        Color white = new Color(1f, 1f, 1f, 1f);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            Vector2 p = Vector2.Lerp(a, b, t);
            float w = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0 : 가운데만 살짝 두껍다
            cells.Add(new KeyValuePair<Vector2, Color>(p, t < 0.9f ? white : edge));
            if (w > 0.6f) cells.Add(new KeyValuePair<Vector2, Color>(p + perp, edge)); // [2026-10-17] 폭 줄임: 최대 2칸
        }
        // 바람 가르는 자국: 칼날 옆에 흐린 한 줄 (짧게, 칼날보다 조금 늦게)
        for (int i = 0; i <= n; i += 2)
        {
            float t = i / (float)n;
            if (t < 0.18f || t > 0.82f) continue;
            cells.Add(new KeyValuePair<Vector2, Color>(Vector2.Lerp(a, b, t) - perp * 3f, Wind));
        }
    }

    private static void Ring(List<KeyValuePair<Vector2, Color>> cells, float r, Color col)
    {
        int n = 6;
        for (int i = -n; i <= n; i++)
        {
            float t = i / (float)n * r;
            cells.Add(new KeyValuePair<Vector2, Color>(new Vector2(t, r), col));
            cells.Add(new KeyValuePair<Vector2, Color>(new Vector2(t, -r), col));
            cells.Add(new KeyValuePair<Vector2, Color>(new Vector2(r, t), col));
            cells.Add(new KeyValuePair<Vector2, Color>(new Vector2(-r, t), col));
        }
    }

    // ─────────────────────────────────────────
    // 3) 처치 — 사각 조각으로 부서짐
    // ─────────────────────────────────────────
    /// <summary>EnemyStats.HandleDeath 가 그림을 끄기 직전에 부른다.</summary>
    public static void Crumble(SpriteRenderer sr)
    {
        var fx = Get();
        if (fx == null || sr == null || sr.sprite == null || !sr.enabled) return;
        fx.StartCoroutine(fx.CrumbleRoutine(sr));
    }

    private IEnumerator CrumbleRoutine(SpriteRenderer src)
    {
        Sprite sp = src.sprite;
        Rect tr;
        try { tr = sp.textureRect; } catch { yield break; } // 촘촘히 묶인 아틀라스 그림은 자를 수 없음 → 연출 없이
        const int N = 8;                                     // 가로·세로 8칸 = 64조각
        float cw = tr.width / N, ch = tr.height / N;
        Bounds b = src.bounds;
        float wx = b.size.x / N, wy = b.size.y / N;
        var bits = new List<Transform>(); var vel = new List<Vector2>(); var rends = new List<SpriteRenderer>(); var delay = new List<float>();
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Sprite piece;
                try { piece = Sprite.Create(sp.texture, new Rect(tr.x + x * cw, tr.y + y * ch, cw, ch), new Vector2(0.5f, 0.5f), sp.pixelsPerUnit); }
                catch { continue; }
                var go = new GameObject("CrumblePx");
                go.transform.SetParent(transform, false);
                int col = src.flipX ? N - 1 - x : x;
                go.transform.position = new Vector3(b.min.x + (col + 0.5f) * wx, b.min.y + (y + 0.5f) * wy, b.center.z);
                Vector3 ls = src.transform.lossyScale;
                go.transform.localScale = new Vector3(src.flipX ? -Mathf.Abs(ls.x) : Mathf.Abs(ls.x), Mathf.Abs(ls.y), 1f);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = piece; r.color = src.color;
                r.sortingLayerID = src.sortingLayerID; r.sortingOrder = src.sortingOrder;
                bits.Add(go.transform); rends.Add(r);
                vel.Add(new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.2f, 0.9f)));
                delay.Add((N - 1 - y) * 0.03f + Random.Range(0f, 0.05f)); // 위쪽부터 무너진다
            }
        const float dur = 0.75f;
        float q = Mathf.Max(0.02f, wx * 0.5f);
        for (float e = 0f; e < dur; e += Time.deltaTime)
        {
            for (int i = 0; i < bits.Count; i++)
            {
                if (bits[i] == null) continue;
                float te = e - delay[i];
                if (te <= 0f) continue;
                vel[i] += new Vector2(0f, -6f) * Time.deltaTime; // 떨어진다
                Vector3 p = bits[i].position + (Vector3)(vel[i] * Time.deltaTime);
                bits[i].position = p;
                // 계단식 투명도 (4단계)
                float k = Mathf.Clamp01(te / (dur - delay[i] + 0.0001f));
                float a = k < 0.25f ? 1f : k < 0.5f ? 0.75f : k < 0.75f ? 0.45f : 0.15f;
                var c = rends[i].color; c.a = a; rends[i].color = c;
            }
            yield return null;
        }
        for (int i = 0; i < bits.Count; i++)
        {
            if (rends[i] != null && rends[i].sprite != null) Destroy(rends[i].sprite);
            if (bits[i] != null) Destroy(bits[i].gameObject);
        }
    }
}
