using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.EditorCoroutines.Editor
{
    /// <summary>
    /// Manages and executes coroutines within the Unity Editor environment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The `EditorCoroutine` class enables running coroutines without entering Play Mode, providing a way to perform
    /// asynchronous tasks within the Unity Editor. This feature is beneficial for developing editor tools that require
    /// non-blocking operations such as waiting on web requests, IO operations or for user inputs.
    /// </para>
    /// <para>
    /// This class interacts with the <a href="https://docs.unity3d.com/ScriptReference/EditorApplication-update.html">EditorApplication.update</a> event to advance coroutine execution.
    /// Coroutines started with `EditorCoroutine` can be controlled via the <see cref="EditorCoroutineUtility"/> methods,
    /// which offer lifecycle control such as stopping or pausing operations.
    /// </para>
    /// <para>
    /// **Tip**: `yield return null` is a way to skip a frame in the Editor.
    /// </para>
    /// <para>
    /// <b>Important Considerations:</b>
    /// <list type="bullet">
    ///     <item>
    ///         <description>Be aware of editor performance when using `EditorCoroutine` for intensive tasks; aim to optimize long-running
    ///   operations to avoid impacting the Unity Editor's responsiveness. Remember that coroutines execute synchronously 
    ///   on the main thread in between of yielding.</description>
    ///     </item>
    ///     <item>
    ///         <description>Coroutines automatically stop if the owner object is garbage collected, thanks to a `WeakReference` ownership check.</description>
    ///     </item>
    ///     <item>
    ///         <description>No explicit update calls are needed as this class handles its integration into the editor's update loop.</description>
    ///     </item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <seealso href="https://docs.unity3d.com/Manual/coroutines.html">Coroutines</seealso>
    /// <example>
    /// <para>
    /// The following example demonstrates how to use `EditorCoroutine` to increment a counter over time:
    /// </para>
    /// <code lang="cs"><![CDATA[
    /// using UnityEditor;
    /// using UnityEngine;
    /// using System.Collections;
    ///  
    /// using System.Collections;
    /// using Unity.EditorCoroutines.Editor;
    /// using UnityEditor;
    /// using UnityEngine;
    ///
    /// public class EditorCoroutineExample : EditorWindow
    /// {
    ///     int m_Counter;
    ///     EditorCoroutine m_Coroutine;
    /// 
    ///     [MenuItem("Window/Editor Coroutine Example")]
    ///     public static void ShowWindow()
    ///     {
    ///         GetWindow<EditorCoroutineExample>("Editor Coroutine Example");
    ///     }
    /// 
    ///     void OnGUI()
    ///     {
    ///         if (GUILayout.Button("Start Coroutine"))
    ///             m_Coroutine = EditorCoroutineUtility.StartCoroutine(IncrementCounter(), this);
    ///         if (GUILayout.Button("Stop Coroutine"))
    ///             EditorCoroutineUtility.StopCoroutine(m_Coroutine);
    ///         GUILayout.Label("Counter: " + m_Counter);
    ///     }
    /// 
    ///     IEnumerator IncrementCounter()
    ///     {
    ///         for (int i = 0; i < 10; i++)
    ///         {
    ///             m_Counter++;
    ///             Debug.Log("Counter: " + m_Counter);
    ///             Repaint();
    ///             yield return new EditorWaitForSeconds(1);
    ///         }
    ///     }
    /// }]]>
    /// </code>
    /// </example>
    public class EditorCoroutine
    {
        private struct YieldProcessor
        {
            enum DataType : byte
            {
                None = 0,
                WaitForSeconds = 1,
                EditorCoroutine = 2,
                AsyncOP = 3,
            }
            struct ProcessorData
            {
                public DataType type;
                public double targetTime;
                public object current;
            }

            ProcessorData data;

            public void Set(object yield)
            {
                if (yield == data.current)
                    return;

                var type = yield.GetType();
                var dataType = DataType.None;
                double targetTime = -1;

                if(type == typeof(EditorWaitForSeconds))
                {
                    targetTime = EditorApplication.timeSinceStartup + (yield as EditorWaitForSeconds).WaitTime;
                    dataType = DataType.WaitForSeconds;
                }
                else if(type == typeof(EditorCoroutine))
                {
                    dataType = DataType.EditorCoroutine;
                }
                else if(type == typeof(AsyncOperation) || type.IsSubclassOf(typeof(AsyncOperation)))
                {
                    dataType = DataType.AsyncOP;
                }

                data = new ProcessorData { current = yield, targetTime = targetTime, type = dataType };
            }

            public bool MoveNext(IEnumerator enumerator)
            {
                bool advance = false;
                switch (data.type)
                {
                    case DataType.WaitForSeconds:
                        advance = data.targetTime <= EditorApplication.timeSinceStartup;
                        break;
                    case DataType.EditorCoroutine:
                        advance = (data.current as EditorCoroutine).m_Status == Status.Done;
                        break;
                    case DataType.AsyncOP:
                        advance = (data.current as AsyncOperation).isDone;
                        break;
                    default:
                        advance = data.current == enumerator.Current; //a IEnumerator or a plain object was passed to the implementation
                        break;
                }

                if(advance)
                {
                    data = default(ProcessorData); 
                    return enumerator.MoveNext();
                }
                return true;
            }
        }

        private enum Status 
        {
            Invalid,
            Stopped,
            Running,
            Done
        }

        internal WeakReference m_Owner;
        IEnumerator m_Routine;
        YieldProcessor m_Processor;

        Status m_Status = Status.Invalid;

        internal EditorCoroutine(IEnumerator routine)
        {
            if (routine == null)
                throw new ArgumentNullException("Argument 'routine' must be non-null");

            m_Owner = null;
            m_Routine = routine;
            m_Status = Status.Running;
            EditorApplication.update += MoveNext;
        }

        internal EditorCoroutine(IEnumerator routine, object owner)
        {
            if (routine == null)
                throw new ArgumentNullException("Argument 'routine' must be non-null");

            m_Processor = new YieldProcessor();
            m_Owner = new WeakReference(owner);
            m_Routine = routine;
            m_Status = Status.Running;
            EditorApplication.update += MoveNext;
        }

        internal void MoveNext()
        {
            if ((m_Owner != null && !m_Owner.IsAlive) || (m_Status != Status.Running))
            {
                EditorApplication.update -= MoveNext;
                return;
            }

            if (!ProcessIEnumeratorRecursive(m_Routine)) 
            {
                m_Status = Status.Done;
                EditorApplication.update -= MoveNext;
            }
        }

        static Stack<IEnumerator> kIEnumeratorProcessingStack = new Stack<IEnumerator>(32);
        private bool ProcessIEnumeratorRecursive(IEnumerator enumerator)
        {
            var root = enumerator;
            while(enumerator.Current as IEnumerator != null)
            {
                kIEnumeratorProcessingStack.Push(enumerator);
                enumerator = enumerator.Current as IEnumerator;
            }

            //process leaf
            m_Processor.Set(enumerator.Current);
            var result = m_Processor.MoveNext(enumerator);

            while (kIEnumeratorProcessingStack.Count > 1)
            {
                if (!result)
                {
                    result = kIEnumeratorProcessingStack.Pop().MoveNext();
                }
                else
                    kIEnumeratorProcessingStack.Clear();
            }

            if (kIEnumeratorProcessingStack.Count > 0 && !result && root == kIEnumeratorProcessingStack.Pop())
            {
                result = root.MoveNext();
            }

            return result;
        }

        internal void Stop()
        {
            m_Owner = null;
            m_Routine = null;
            m_Status = Status.Stopped;
            EditorApplication.update -= MoveNext;
        }
    }
}
