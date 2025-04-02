using UnityEngine;

namespace Unity.BossRoom.Utils
{
    /// <summary>
    /// Utility struct to linearly interpolate between two Vector3 values. Allows for flexible linear interpolations
    /// where current and target change over time.
    /// </summary>
    /// <summary>
    /// 두 개의 Vector3 값 사이를 선형 보간하는 유틸리티 구조체입니다.
    /// 현재 값과 목표 값이 시간에 따라 변경될 수 있는 유연한 선형 보간을 제공합니다.
    /// </summary>
    public struct PositionLerper
    {
        // Calculated start for the most recent interpolation
        // 가장 최근 보간의 시작 위치
        Vector3 m_LerpStart;

        // Calculated time elapsed for the most recent interpolation
        // 가장 최근 보간에서 경과된 시간
        float m_CurrentLerpTime;

        // The duration of the interpolation, in seconds
        // 보간 지속 시간 (초 단위)
        float m_LerpTime;

        public PositionLerper(Vector3 start, float lerpTime)
        {
            m_LerpStart = start;
            m_CurrentLerpTime = 0f;
            m_LerpTime = lerpTime;
        }

        /// <summary>
        /// Linearly interpolate between two Vector3 values.
        /// </summary>
        /// <param name="current"> Start of the interpolation. </param>
        /// <param name="target"> End of the interpolation. </param>
        /// <returns> A Vector3 value between current and target. </returns>
        /// <summary>
        /// 두 개의 Vector3 값 사이를 선형 보간합니다.
        /// </summary>
        /// <param name="current"> 보간의 시작 위치. </param>
        /// <param name="target"> 보간의 목표 위치. </param>
        /// <returns> 현재 값과 목표 값 사이의 Vector3 값. </returns>
        public Vector3 LerpPosition(Vector3 current, Vector3 target)
        {
            if (current != target)
            {
                m_LerpStart = current;
                m_CurrentLerpTime = 0f;
            }

            m_CurrentLerpTime += Time.deltaTime;
            if (m_CurrentLerpTime > m_LerpTime)
            {
                m_CurrentLerpTime = m_LerpTime;
            }

            var lerpPercentage = m_CurrentLerpTime / m_LerpTime;

            return Vector3.Lerp(m_LerpStart, target, lerpPercentage);
        }
    }
}
