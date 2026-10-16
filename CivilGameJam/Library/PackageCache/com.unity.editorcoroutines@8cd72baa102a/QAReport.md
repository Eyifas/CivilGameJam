# Quality Report
Use this file to outline the test strategy for this package.

## Version tested: 1.0.0
## Date: 2020-03-23
## QA Owner: Richard Pickering
## UX Owner: Not applicable

## Test strategy
*The Editor Coroutines package contains a scripting API which is covered by the following in-editor tests*:

1. Coroutine_LogsStepsAtExpectedTimes -> Verifies that a coroutine is resuming execution at the expected time after yielding a EditorWaitForSeconds instructions.
2. Coroutine_WaitsForSpecifiedNumberOfSeconds -> Verifies that a coroutine can yield EditorWaitForSeconds instructions. 
3. CoroutineWithAbitraryObject_StopsExecutionIfObjectIsCollected -> Verifies that a coroutine owned by a vanila C# object(non-UnityEngine.Object derived) will stop execution after the object is collected by the GC.
4. CoroutineWithAbitraryUnityEngineObject_StopsExecutionIfObjectIsCollected -> Verifies that a coroutine owned by a UnityEngine C# object(UnityEngine.Object derived) will stop execution after the object is destroyed. This test also ensures parity with the MonoBehaviour coroutines's shutting down when their script is destroyed.
5. NestedCoroutinesWithoutOwner_WaitForSpecificNumberOfSeconds -> Verifies that a ownerless coroutine can yield another ownerless Editor Coroutine.
6. NestedCoroutinesWithoutOwner_WaitForSpecificNumberOfSeconds -> Verifies that a coroutine can yield another owned Editor Coroutine.
7. CoroutineWithoutOwner_YieldingIEnumerator -> Verifies that a ownerless coroutine executes and logs data in the expected order.
8. ThrowingCoroutine_DoesNotHandleExitGUIException -> Verifies that we do not handle ExitGUI exceptions as that would cause EditorWindow functionality to break. Furthermore we should not catch any exception inside a coroutine as that in general is disallowed for IEnumerator collections.

*Test results link*: [Yamato](https://yamato.prd.cds.internal.unity3d.com/jobs/59-EditorCoroutines/tree/master/.yamato%252Fupm-ci.yml%2523test_trigger/1702007/job)

## Package Status
No known issues have been found with the package implementation. API stability is proven by unit tests. Furthermore it is used inside the Memory Profiler package's core functionality. 

