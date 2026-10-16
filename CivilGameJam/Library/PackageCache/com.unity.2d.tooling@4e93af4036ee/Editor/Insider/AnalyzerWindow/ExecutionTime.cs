using System;
using UnityEngine;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    class ExecutionTime : IDisposable
    {
        static bool s_EnableExecutionTimeLogging = false;
        string m_Name;
        DateTime m_StartTime;
        public ExecutionTime(string name)
        {
            m_Name = name;
            m_StartTime = DateTime.Now;
        }

        public void Dispose()
        {
            if (s_EnableExecutionTimeLogging)
            {
                var elapsedTime = DateTime.Now - m_StartTime;
                Debug.Log($"{m_Name} took {elapsedTime.TotalMilliseconds} ms");
            }
        }

        [MenuItem("internal:2D/Tooling/Execution Time Logging")]
        static void EnableExecutionTimeLogging()
        {
            s_EnableExecutionTimeLogging = !s_EnableExecutionTimeLogging;
            Menu.SetChecked("2D/Tooling/Execution Time Logging", s_EnableExecutionTimeLogging);
        }
    }
}
