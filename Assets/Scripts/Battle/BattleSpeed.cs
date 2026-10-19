using UnityEngine;

/// <summary>
/// [2026-10-20] 전투 속도 — 드래곤 퀘스트 5 의 "메시지 속도" 방식.
///  - 단계 1(빠름) ~ 5(느림), 기본 3. PlayerPrefs 에 저장 (세이브 파일과 별개 — 기기 설정).
///  - 해결 단계에서 마우스 왼쪽 / 스페이스 / 엔터를 누르고 있으면 그동안 3배 빨라진다 (드퀘 SFC 연타 느낌).
/// 박자 길이 자체는 BattleManager.DeclareHold / ResultHold.
/// </summary>
public static class BattleSpeed
{
    public const int Min = 1, Max = 5, Default = 3;
    private const string PrefKey = "BattleSpeed";
    private static readonly float[] Scales = { 0.45f, 0.7f, 1.0f, 1.3f, 1.7f };
    public const float HoldBoost = 3f;

    public static int Level
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, Default), Min, Max); }
        set { PlayerPrefs.SetInt(PrefKey, Mathf.Clamp(value, Min, Max)); PlayerPrefs.Save(); }
    }

    /// <summary>박자 시간에 곱하는 배율 (단계 3 = 1.0).</summary>
    public static float Scale => Scales[Level - 1];

    /// <summary>지금 "빨리 넘기기" 입력을 누르고 있는지.</summary>
    public static bool FastHeld =>
        Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
}
