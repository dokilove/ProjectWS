using System.Diagnostics;

/// <summary>
/// 피격/충돌처럼 매우 자주 발생하는 전투 로그용 헬퍼입니다.
/// [Conditional] 덕분에 COMBAT_DEBUG_LOG 심볼이 없으면 호출 자체(문자열 보간 포함)가 컴파일에서 제거됩니다.
/// 로그가 필요하면 Project Settings > Player > Scripting Define Symbols에 COMBAT_DEBUG_LOG를 추가하세요.
/// </summary>
public static class CombatLog
{
    [Conditional("COMBAT_DEBUG_LOG")]
    public static void Log(string message, UnityEngine.Object context = null)
    {
        UnityEngine.Debug.Log(message, context);
    }
}
