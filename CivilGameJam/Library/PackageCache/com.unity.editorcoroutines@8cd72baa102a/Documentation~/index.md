# Editor coroutines

The Editor coroutines package enables the execution of [iterator methods](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/yield) in the Unity Editor's Edit mode, in a similar way to running [coroutines](https://docs.unity3d.com/Manual/Coroutines.html) inside [MonoBehaviour](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html) scripts at runtime.

## Installation

To install the Editor coroutines package, follow the instructions in [Add and remove UPM packages or featuresets](https://docs.unity3d.com/Manual/upm-ui-actions.html). 

## Using Editor coroutines

The Editor coroutines package is currently documented as API documentation only. For more information on using Editor coroutines, including code examples, refer to the [scripting API](xref:Unity.EditorCoroutines.Editor.EditorCoroutine) section of the documentation.

## Requirements

This version of Editor coroutines is compatible with the following versions of the Unity Editor:

* 2019.4 and later (recommended)

> **Note**:  If you install the [Memory Profiler](https://docs.unity3d.com/Packages/com.unity.memoryprofiler@latest) package it will automatically install the Editor coroutines package as a dependency.

## Known limitations

The iterator functions passed to Editor coroutines do not support yielding any of the instruction classes derived from [`YieldInstruction`](https://docs.unity3d.com/ScriptReference/YieldInstruction.html), such as [`WaitForSeconds`](https://docs.unity3d.com/ScriptReference/WaitForSeconds.html) and [`WaitForEndOfFrame`](https://docs.unity3d.com/ScriptReference/WaitForEndOfFrame.html). The only exception to this is classes derived from [`CustomYieldInstruction`](https://docs.unity3d.com/ScriptReference/CustomYieldInstruction.html) with the [`MoveNext`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.ienumerator.movenext?view=net-9.0) method implemented.

## Additional resources

- [Runtime coroutines](https://docs.unity3d.com/Manual/Coroutines.html)
- [Yield statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/yield)