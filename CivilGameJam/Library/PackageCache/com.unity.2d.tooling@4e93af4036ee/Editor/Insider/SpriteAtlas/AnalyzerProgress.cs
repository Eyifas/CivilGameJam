using System;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    class AnalyzerProgress
    {
        int m_ID;

        public void StartProgressTrack(string progressTracker)
        {
            m_ID = Progress.Start(progressTracker);
        }

        public void UpdateProgressTrack(int currentStep, int totalStep, string message)
        {
            Progress.Report(m_ID, currentStep, totalStep, message);
        }

        public void UpdateProgressTrack(float progress, string message)
        {
            Progress.Report(m_ID, progress, message);
        }

        public void EndProgressTrack()
        {
            Progress.Remove(m_ID);
        }
    }
}
