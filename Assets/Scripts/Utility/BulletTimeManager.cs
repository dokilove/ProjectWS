using UnityEngine;
using System.Collections;
using System;

namespace ProjectWS.Utility
{
    public class BulletTimeManager : MonoBehaviour
    {
        public static BulletTimeManager Instance { get; private set; }

        public bool IsBulletTimeActive { get; private set; } = false;
        public event Action OnBulletTimeEnd;

        private float _originalTimeScale;
        private float _originalFixedDeltaTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 유지되도록 설정
            }
        }

        /// <summary>
        /// 불릿 타임을 시작합니다.
        /// </summary>
        /// <param name="duration">불릿 타임 지속 시간 (실제 시간 기준)</param>
        /// <param name="newTimeScale">새로운 Time Scale 값 (예: 0.2f)</param>
        public void StartBulletTime(float duration, float newTimeScale)
        {
            if (IsBulletTimeActive) return;

            StartCoroutine(BulletTimeCoroutine(duration, newTimeScale));
        }

        private IEnumerator BulletTimeCoroutine(float duration, float newTimeScale)
        {
            IsBulletTimeActive = true;

            _originalTimeScale = Time.timeScale;
            _originalFixedDeltaTime = Time.fixedDeltaTime;

            Time.timeScale = newTimeScale;
            // 물리 업데이트 시간도 Time Scale에 맞춰 조정해야 Rigidbody가 부드럽게 움직입니다.
            Time.fixedDeltaTime = _originalFixedDeltaTime * Time.timeScale;

            // WaitForSeconds는 Time.timeScale의 영향을 받으므로, 실제 시간 기준으로 기다리려면 WaitForSecondsRealtime을 사용합니다.
            yield return new WaitForSecondsRealtime(duration);

            // 불릿 타임 종료
            Time.timeScale = _originalTimeScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime;

            IsBulletTimeActive = false;
            OnBulletTimeEnd?.Invoke(); // 불릿 타임 종료 이벤트 호출
        }
    }
}
