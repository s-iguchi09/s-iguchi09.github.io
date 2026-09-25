---
layout: app-page
permalink: /apps/wpf-messagebox-sample-creator.html
title: "WPF MessageBox Sample Creator | Preview MessageBox Settings Live"
heading: "WPF MessageBox Sample Creator"
lead: "Real-time simulation of the standard Windows <code>MessageBox</code>. Pre-built .exe available on GitHub—start UI specification reviews without a dev environment."
description: "A tool to configure and preview WPF MessageBox live and compare MessageBoxButton and MessageBoxImage combinations. A pre-built exe is on GitHub Releases."
---

## Overview: Interactive MessageBox Simulator

The WPF MessageBox Sample Creator is a prototyping application that allows you to intuitively customize the standard `MessageBox` and preview its actual behavior instantly. Set the message text, caption, button set, icon, default result and display options, then show the dialog with exactly those settings.

**Since pre-built executable files (.exe) are available on the GitHub Releases page, anyone can use the tool by simply downloading it—no Visual Studio or development environment required.** The builds are framework-dependent, so the matching x64 .NET Desktop Runtime does have to be installed already; see [System Requirements](#requirements) below.

![WPF MessageBox Sample Creator main window with input fields for message text, caption, buttons, icon, and options](/attachments/WPFMessageBoxSampleCreator.png){: .screenshot-img}

## Background: Removing the “Build Barrier”

In WPF development, repeatedly writing code just to check the behavior of dialogs is inefficient. Furthermore, designers and architects often find it difficult to use tools that are only shared as source code, as setting up a build environment is a significant hurdle.

To solve this, this tool is distributed in a ready-to-run format. The tool uses WPF alone, with no third-party libraries and no custom styles, so the ZIP of the latest release, v1.0.0.2, is about 200 KB.

## Configurable Parameters

The arguments of the `MessageBox.Show` overload this tool calls are each exposed as an independent input, so combinations can be tried without editing and rebuilding code. The overloads that take an owner `Window` as their first argument are out of scope — the tool does not set an owner:

| Parameter | Type | Effect |
| --- | --- | --- |
| Message text | `string` | The body of the dialog. Useful for checking how long text wraps and how the dialog resizes. |
| Caption | `string` | The title bar text. |
| Buttons | `MessageBoxButton` | The button set. The list is the enum's members on the runtime in use: the .NET 8 build offers four (OK, OKCancel, YesNoCancel, YesNo), and the .NET 10 build seven, adding AbortRetryIgnore, RetryCancel, and CancelTryContinue. |
| Icon | `MessageBoxImage` | The system icon shown beside the message. All nine member names are selectable. |
| Default result | `MessageBoxResult` | Which button is the default when the dialog opens — that is, what Enter activates. Every member of the enum is selectable here (ten on .NET 10), but the value only takes effect when the chosen button set contains that button. Showing every button set with every result on .NET 8.0.31 and 10.0.10, a result the dialog did not display always made the first button the default. |
| Options | `MessageBoxOptions` | Display options such as right-aligned text and right-to-left reading order. The list holds the enum's five members and one is chosen at a time, so flags cannot be combined here. |

## What the Tool Makes Obvious: Nine Icon Names, Four Icons

`MessageBoxImage` has nine members, which suggests nine different icons are available. It is not the case. Several members share the same underlying numeric value, so they are indistinguishable at runtime — the enum simply offers alternative names for the same Win32 flag:

| Enum members | Value | Icon actually displayed |
| --- | --- | --- |
| `None` | 0 | No icon |
| `Error` / `Hand` / `Stop` | 16 | White X in a red circle |
| `Question` | 32 | Question mark in a circle |
| `Exclamation` / `Warning` | 48 | Exclamation point in a yellow triangle |
| `Asterisk` / `Information` | 64 | Lowercase i in a circle |

The duplicates also show up in code. On .NET 8.0.31 and 10.0.10, the values from `Enum.GetValues` converted to strings as `Hand` three times, `Exclamation` twice, and `Asterisk` twice. A value shared by several names converts to only one of them, and .NET does not specify which, so another runtime may show a different name. Either way, a ComboBox bound to it would list the same name several times, which is why the tool builds its icon list from the nine names instead.

Selecting `Error`, `Hand` and `Stop` in turn and seeing the identical dialog each time settles a question that reading the enum definition tends to raise. It also means picking between those names is a readability decision for the code, not a visual one for the user.

One further caveat worth knowing before you reach for it: Microsoft documents the `Question` icon as no longer recommended, on the grounds that a question mark does not identify a category of message and can be mistaken for a help affordance. It remains in the enum for backward compatibility. For a confirmation prompt, `Warning` or no icon at all is the safer choice.

## How the Tool Is Built

The **Show MessageBox.** button makes a single call, `MessageBox.Show(text, caption, button, icon, defaultResult, options)`, with the six values held by the view model. The lists for the button set, the default result, and the options are `Enum.GetValues` results bound through `ObjectDataProvider`, so they follow the enum of the runtime the build targets. That is why the .NET 10 build offers button sets that the .NET 8 build does not. The icon list is the exception described above.

The window uses WPF's default styles. There is no `Style` in its XAML.

## Typical Uses

- **Settling dialog wording in a spec review.** Show the actual dialog to reviewers instead of describing it, and agree on the text and button set in the meeting itself.
- **Checking how long messages render.** A message that reads fine in code can wrap awkwardly or stretch the dialog once displayed. Pasting the real string in shows the result immediately.
- **Confirming which button Enter triggers.** Getting the default result wrong on a destructive confirmation is a real usability defect; the tool shows where focus lands before the code is written.
- **Verifying right-to-left layout.** The options parameter covers RTL reading order, which is otherwise awkward to check.

## System Requirements {#requirements}

- **OS:** Windows, x64
- **Runtime:** The ZIP of the latest release, v1.0.0.2, contains two win-x64 builds, one for .NET 8 and one for .NET 10. Both are framework-dependent — the runtime is not bundled — so the matching x64 **.NET Desktop Runtime** has to be installed. Pick the executable for whichever version you already have.
- **UI framework:** WPF, no third-party dependencies

## Downloads & Repository

You can download the executable or browse the source code on GitHub:

[🚀 Download Pre-built .exe (GitHub Releases)](https://github.com/s-iguchi09/WPFMessageBoxSampleCreator/releases){: .github-link target="_blank" rel="noopener noreferrer"}

[View GitHub Repository (Source Code)](https://github.com/s-iguchi09/WPFMessageBoxSampleCreator){: target="_blank" rel="noopener noreferrer"}
