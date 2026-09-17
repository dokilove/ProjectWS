using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeleeData))]
public class MeleeDataEditor : Editor
{
    private SerializedProperty _comboStepsProp;
    private SerializedProperty _chargeAttackProp;
    private SerializedProperty _lockOnRadiusProp;

    private readonly List<bool> _comboFoldouts = new List<bool>();
    private bool _chargeFoldout = true;

    private void OnEnable()
    {
        _comboStepsProp = serializedObject.FindProperty("comboSteps");
        _chargeAttackProp = serializedObject.FindProperty("chargeAttack");
        _lockOnRadiusProp = serializedObject.FindProperty("meleeLockOnRadius");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 헤더
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚔ Melee Combat Data Configuration", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("단발/다단히트 및 공격 형태(부채꼴/박스/원형)에 따라 필요한 설정만 깔끔하게 표시됩니다.", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // 1. 락온 반경
        EditorGUILayout.PropertyField(_lockOnRadiusProp, new GUIContent("근접 락온 반경 (Lock-On Radius)"));
        EditorGUILayout.Space(8);

        // 2. 콤보 스텝 목록
        DrawComboStepsSection();

        EditorGUILayout.Space(10);

        // 3. 차지 공격 섹션
        DrawChargeAttackSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawComboStepsSection()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"콤보 공격 단계 ({_comboStepsProp.arraySize}단계)", EditorStyles.boldLabel);

        if (GUILayout.Button("+ 단계 추가", EditorStyles.miniButton, GUILayout.Width(80)))
        {
            _comboStepsProp.InsertArrayElementAtIndex(_comboStepsProp.arraySize);
            var newElem = _comboStepsProp.GetArrayElementAtIndex(_comboStepsProp.arraySize - 1);
            newElem.FindPropertyRelative("stepName").stringValue = $"Combo {_comboStepsProp.arraySize}";
            newElem.FindPropertyRelative("shape").enumValueIndex = 0;
            newElem.FindPropertyRelative("attackRadius").floatValue = 5f;
            newElem.FindPropertyRelative("attackAngle").floatValue = 90f;
            newElem.FindPropertyRelative("boxWidth").floatValue = 2f;
            newElem.FindPropertyRelative("boxLength").floatValue = 4f;
            newElem.FindPropertyRelative("forwardOffset").floatValue = 0f;
            newElem.FindPropertyRelative("damage").floatValue = 20f;
            newElem.FindPropertyRelative("hitStunDuration").floatValue = 0.2f;
            newElem.FindPropertyRelative("dashForce").floatValue = 100f;
            newElem.FindPropertyRelative("useMultiHit").boolValue = false;
            newElem.FindPropertyRelative("subHits").ClearArray();
            newElem.FindPropertyRelative("hitDelay").floatValue = 0.08f;
            newElem.FindPropertyRelative("cancelWindowTime").floatValue = 0.2f;
            newElem.FindPropertyRelative("totalDuration").floatValue = 0.35f;
            newElem.FindPropertyRelative("comboResetTime").floatValue = 0.6f;
        }
        EditorGUILayout.EndHorizontal();

        while (_comboFoldouts.Count < _comboStepsProp.arraySize)
        {
            _comboFoldouts.Add(true);
        }

        for (int i = 0; i < _comboStepsProp.arraySize; i++)
        {
            SerializedProperty stepProp = _comboStepsProp.GetArrayElementAtIndex(i);
            DrawSingleComboStep(stepProp, i);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSingleComboStep(SerializedProperty stepProp, int index)
    {
        SerializedProperty nameProp = stepProp.FindPropertyRelative("stepName");
        SerializedProperty shapeProp = stepProp.FindPropertyRelative("shape");
        SerializedProperty radiusProp = stepProp.FindPropertyRelative("attackRadius");
        SerializedProperty angleProp = stepProp.FindPropertyRelative("attackAngle");
        SerializedProperty widthProp = stepProp.FindPropertyRelative("boxWidth");
        SerializedProperty lengthProp = stepProp.FindPropertyRelative("boxLength");
        SerializedProperty offsetProp = stepProp.FindPropertyRelative("forwardOffset");
        SerializedProperty damageProp = stepProp.FindPropertyRelative("damage");
        SerializedProperty hitStunProp = stepProp.FindPropertyRelative("hitStunDuration");
        SerializedProperty dashProp = stepProp.FindPropertyRelative("dashForce");
        SerializedProperty matProp = stepProp.FindPropertyRelative("overrideMaterial");
        SerializedProperty multiHitProp = stepProp.FindPropertyRelative("useMultiHit");
        SerializedProperty subHitsProp = stepProp.FindPropertyRelative("subHits");
        SerializedProperty hitDelayProp = stepProp.FindPropertyRelative("hitDelay");
        SerializedProperty cancelTimeProp = stepProp.FindPropertyRelative("cancelWindowTime");
        SerializedProperty durationProp = stepProp.FindPropertyRelative("totalDuration");
        SerializedProperty resetTimeProp = stepProp.FindPropertyRelative("comboResetTime");

        MeleeHitboxShape currentShape = (MeleeHitboxShape)shapeProp.enumValueIndex;
        string modeLabel = multiHitProp.boolValue ? $"[다단히트 {subHitsProp.arraySize}연타]" : $"({currentShape}) [단발]";

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        _comboFoldouts[index] = EditorGUILayout.Foldout(_comboFoldouts[index], $"{index + 1}타: {nameProp.stringValue} {modeLabel}", true, EditorStyles.foldoutHeader);

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("✕ 삭제", EditorStyles.miniButton, GUILayout.Width(55)))
        {
            _comboStepsProp.DeleteArrayElementAtIndex(index);
            _comboFoldouts.RemoveAt(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (_comboFoldouts[index])
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(nameProp, new GUIContent("단계 이름"));

            // 1. 다단히트 토글
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("타격 모드 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(multiHitProp, new GUIContent("다단히트 활성화 (Use Multi-Hit)"));

            if (multiHitProp.boolValue)
            {
                // [다단히트 모드]: 상단 판정 형태/범위 및 데미지/선딜 전부 숨김!
                EditorGUILayout.HelpBox("다단히트 모드 활성화됨: 각 타격의 타이밍, 데미지, 형태 및 범위를 아래 [Sub Hits] 목록에서 개별 설정합니다.", MessageType.Info);
                DrawSubHitsList(subHitsProp);
            }
            else
            {
                // [단발 모드]: 상단 판정 형태/범위 및 데미지/선딜 노출
                DrawShapeFields(shapeProp, radiusProp, angleProp, widthProp, lengthProp, offsetProp);

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("위력 및 선딜레이", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(damageProp, new GUIContent("단발 데미지 (Damage)"));
                EditorGUILayout.PropertyField(hitStunProp, new GUIContent("피격 경직 시간 (Hit Stun)", "적중 시 적이 멈추는 경직 시간 (초)"));
                EditorGUILayout.PropertyField(hitDelayProp, new GUIContent("선딜레이 (Hit Delay)", "모션 시작 후 데미지가 터지는 시간 (초)"));
            }

            // 2. 공통 이동 대시 & 이펙트
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("이동 및 비주얼", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(dashProp, new GUIContent("공격 시작 대시 (Dash Force)"));
            EditorGUILayout.PropertyField(matProp, new GUIContent("기본 비주얼 머티리얼 (선택)"));

            // 3. 타이밍 & 캔슬 제어
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("타이밍 및 콤보 윈도우", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cancelTimeProp, new GUIContent("캔슬 윈도우 시작 (Cancel Time)", "다음 콤보 입력이나 회피로 캔슬 가능한 시점 (초)"));
            EditorGUILayout.PropertyField(durationProp, new GUIContent("전체 동작 시간 (Total Duration)", "모션 완료 및 복귀 시간 (초)"));
            EditorGUILayout.PropertyField(resetTimeProp, new GUIContent("콤보 리셋 시간 (Reset Time)", "다음 입력을 기다리는 제한 시간 (초)"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawChargeAttackSection()
    {
        SerializedProperty shapeProp = _chargeAttackProp.FindPropertyRelative("shape");
        SerializedProperty radiusProp = _chargeAttackProp.FindPropertyRelative("attackRadius");
        SerializedProperty angleProp = _chargeAttackProp.FindPropertyRelative("attackAngle");
        SerializedProperty widthProp = _chargeAttackProp.FindPropertyRelative("boxWidth");
        SerializedProperty lengthProp = _chargeAttackProp.FindPropertyRelative("boxLength");
        SerializedProperty offsetProp = _chargeAttackProp.FindPropertyRelative("forwardOffset");
        SerializedProperty damageProp = _chargeAttackProp.FindPropertyRelative("damage");
        SerializedProperty hitStunProp = _chargeAttackProp.FindPropertyRelative("hitStunDuration");
        SerializedProperty dashProp = _chargeAttackProp.FindPropertyRelative("dashForce");
        SerializedProperty matProp = _chargeAttackProp.FindPropertyRelative("overrideMaterial");
        SerializedProperty multiHitProp = _chargeAttackProp.FindPropertyRelative("useMultiHit");
        SerializedProperty subHitsProp = _chargeAttackProp.FindPropertyRelative("subHits");
        SerializedProperty thresholdProp = _chargeAttackProp.FindPropertyRelative("chargeTimeThreshold");
        SerializedProperty hitDelayProp = _chargeAttackProp.FindPropertyRelative("hitDelay");
        SerializedProperty durationProp = _chargeAttackProp.FindPropertyRelative("totalDuration");

        MeleeHitboxShape currentShape = (MeleeHitboxShape)shapeProp.enumValueIndex;
        string modeLabel = multiHitProp.boolValue ? $"[다단히트 {subHitsProp.arraySize}연타]" : $"({currentShape}) [단발]";

        EditorGUILayout.BeginVertical(GUI.skin.box);
        _chargeFoldout = EditorGUILayout.Foldout(_chargeFoldout, $"⚡ 차지 공격 설정 (Charge Attack) {modeLabel}", true, EditorStyles.foldoutHeader);

        if (_chargeFoldout)
        {
            EditorGUI.indentLevel++;

            // 1. 다단히트 토글
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("타격 모드 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(multiHitProp, new GUIContent("다단히트 활성화 (Use Multi-Hit)"));

            if (multiHitProp.boolValue)
            {
                // [다단히트 모드]: 상단 판정 형태/범위 및 데미지/선딜 전부 숨김!
                EditorGUILayout.HelpBox("다단히트 모드 활성화됨: 차지 타격의 타이밍, 데미지, 형태 및 범위를 아래 [Sub Hits] 목록에서 개별 설정합니다.", MessageType.Info);
                DrawSubHitsList(subHitsProp);
            }
            else
            {
                DrawShapeFields(shapeProp, radiusProp, angleProp, widthProp, lengthProp, offsetProp);

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("위력 및 선딜레이", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(damageProp, new GUIContent("차지 단발 데미지 (Damage)"));
                EditorGUILayout.PropertyField(hitStunProp, new GUIContent("차지 경직 시간 (Hit Stun)", "차지 적중 시 적이 멈추는 경직 시간 (초)"));
                EditorGUILayout.PropertyField(hitDelayProp, new GUIContent("선딜레이 (Hit Delay)"));
            }

            // 2. 대시 & 머티리얼
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("이동 및 비주얼", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(dashProp, new GUIContent("차지 시작 대시 (음수=백스텝)"));
            EditorGUILayout.PropertyField(matProp, new GUIContent("기본 비주얼 머티리얼 (선택)"));

            // 3. 차지 전용 타이밍
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("차지 타이밍 제어", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(thresholdProp, new GUIContent("차지 최소 홀드 시간 (Threshold)", "차지 공격 발동에 필요한 누름 시간 (초)"));
            EditorGUILayout.PropertyField(durationProp, new GUIContent("전체 동작 시간 (Total Duration)"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Shape(부채꼴/박스/원형)에 따라 필요한 필드만 깔끔하게 표시
    /// </summary>
    private void DrawShapeFields(
        SerializedProperty shapeProp,
        SerializedProperty radiusProp,
        SerializedProperty angleProp,
        SerializedProperty widthProp,
        SerializedProperty lengthProp,
        SerializedProperty offsetProp)
    {
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("공격 판정 형태 및 범위", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(shapeProp, new GUIContent("판정 형태 (Shape)"));

        MeleeHitboxShape shape = (MeleeHitboxShape)shapeProp.enumValueIndex;

        switch (shape)
        {
            case MeleeHitboxShape.Sector:
                EditorGUILayout.PropertyField(radiusProp, new GUIContent("공격 사거리 (Radius)"));
                EditorGUILayout.Slider(angleProp, 0f, 360f, new GUIContent("부채꼴 각도 (Angle)"));
                break;

            case MeleeHitboxShape.Box:
                EditorGUILayout.PropertyField(widthProp, new GUIContent("박스 너비 (Width)"));
                EditorGUILayout.PropertyField(lengthProp, new GUIContent("박스 전방 길이 (Length)"));
                EditorGUILayout.PropertyField(offsetProp, new GUIContent("전방 시작 오프셋 (Offset)"));
                break;

            case MeleeHitboxShape.Circle:
                EditorGUILayout.PropertyField(radiusProp, new GUIContent("원형 반경 (Radius)"));
                EditorGUILayout.PropertyField(offsetProp, new GUIContent("전방 중심 오프셋 (Offset)"));
                break;
        }
    }

    /// <summary>
    /// 다단히트 서브히트 목록 그리기
    /// </summary>
    private void DrawSubHitsList(SerializedProperty subHitsProp)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"다단히트 목록 (총 {subHitsProp.arraySize}타)", EditorStyles.boldLabel);

        if (GUILayout.Button("+ 타격 추가", EditorStyles.miniButton, GUILayout.Width(75)))
        {
            subHitsProp.InsertArrayElementAtIndex(subHitsProp.arraySize);
            var newHit = subHitsProp.GetArrayElementAtIndex(subHitsProp.arraySize - 1);
            newHit.FindPropertyRelative("hitTime").floatValue = (subHitsProp.arraySize) * 0.1f;
            newHit.FindPropertyRelative("damage").floatValue = 15f;
            newHit.FindPropertyRelative("hitStunDuration").floatValue = 0.15f;
            newHit.FindPropertyRelative("dashForce").floatValue = 0f;
            newHit.FindPropertyRelative("shape").enumValueIndex = 0;
            newHit.FindPropertyRelative("attackRadius").floatValue = 5f;
            newHit.FindPropertyRelative("attackAngle").floatValue = 90f;
            newHit.FindPropertyRelative("boxWidth").floatValue = 2f;
            newHit.FindPropertyRelative("boxLength").floatValue = 4f;
            newHit.FindPropertyRelative("forwardOffset").floatValue = 0f;
        }
        EditorGUILayout.EndHorizontal();

        for (int j = 0; j < subHitsProp.arraySize; j++)
        {
            SerializedProperty hitElem = subHitsProp.GetArrayElementAtIndex(j);
            SerializedProperty timeProp = hitElem.FindPropertyRelative("hitTime");
            SerializedProperty dmgProp = hitElem.FindPropertyRelative("damage");
            SerializedProperty hitStunProp = hitElem.FindPropertyRelative("hitStunDuration");
            SerializedProperty subDashProp = hitElem.FindPropertyRelative("dashForce");
            SerializedProperty subMatProp = hitElem.FindPropertyRelative("overrideMaterial");
            SerializedProperty subShapeProp = hitElem.FindPropertyRelative("shape");
            SerializedProperty subRadiusProp = hitElem.FindPropertyRelative("attackRadius");
            SerializedProperty subAngleProp = hitElem.FindPropertyRelative("attackAngle");
            SerializedProperty subWidthProp = hitElem.FindPropertyRelative("boxWidth");
            SerializedProperty subLengthProp = hitElem.FindPropertyRelative("boxLength");
            SerializedProperty subOffsetProp = hitElem.FindPropertyRelative("forwardOffset");

            MeleeHitboxShape subShape = (MeleeHitboxShape)subShapeProp.enumValueIndex;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"#{j + 1}타  ({timeProp.floatValue:F2}초 / {subShape} / 데미지 {dmgProp.floatValue})", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(25)))
            {
                subHitsProp.DeleteArrayElementAtIndex(j);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(timeProp, new GUIContent("발생 시점 (Hit Time)", "모션 시작(0초) 후 이 타격이 터지는 시점 (초)"));
            EditorGUILayout.PropertyField(dmgProp, new GUIContent("타격 데미지 (Damage)"));
            EditorGUILayout.PropertyField(hitStunProp, new GUIContent("피격 경직 시간 (Hit Stun)", "이 타격 적중 시 적이 멈추는 경직 시간 (초)"));
            EditorGUILayout.PropertyField(subDashProp, new GUIContent("순간 대시력 (Dash Force)", "이 타격 시 순간적으로 가해지는 전진/후진 힘"));
            EditorGUILayout.PropertyField(subMatProp, new GUIContent("비주얼 머티리얼 (선택)"));

            // 서브 히트별 형태 및 범위 설정
            DrawShapeFields(subShapeProp, subRadiusProp, subAngleProp, subWidthProp, subLengthProp, subOffsetProp);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }
}
