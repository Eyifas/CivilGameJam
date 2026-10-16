using System.Threading.Tasks;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    /// <summary>
    /// Provides utility methods for opening analyzer windows and performing analysis operations.
    /// </summary>
    public class AnalyzerUtility
    {
        /// <summary>
        /// Opens a new analyzer window and performs analysis on the specified paths.
        /// </summary>
        /// <param name="path">An array of file or directory paths to analyze.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// instance that provides access to the analysis results.
        /// </returns>
        public static async Task<EditorWindow> OpenWindowAndAnalyze(string[] path)
        {
            var window = EditorWindow.CreateWindow<AnalyzerWindow>();
            window.Show();
            await window.Analyze(path);
            return window;
        }
    }
}
