# Engine execution incident restriction

The user reported about twenty repeated Unreal security dialogs blocking unrelated work on 2026-10-23 and confirmed Windows firewall blocked-feature prompts. Firewall records show 20 distinct private packaged executable paths and 40 associated rules. See `artifacts/security-dialog-firewall-rules.json` and `artifacts/security-dialog-firewall-events.json`. The user approved one UAC installation. The exact fixed-path private runtime inbound block rule is installed and verified in artifacts/offline-runtime-firewall-installation.json. Shared engine and global firewall policies remain unchanged.

- Keep `.engine-execution-blocked` in place. Do not launch Unity, Unreal, packaged runtime games, RunUAT, or engine build workflows, including through old published CLI binaries or ad hoc shell/Python commands.
- Do not remove or bypass the block simply to continue the persistent development goal. Resolve the security-dialog cause with concrete evidence first, and ensure the proposed execution cannot interrupt other work before resuming.
- Continue source edits and engine-free tests. Do not disable Windows security, suppress global notifications, or add broad firewall exceptions.
- Do not terminate or alter unrelated projects or processes. Do not use computer-use tools.
- Incident verification: `artifacts/security-dialog-guard-verification.json`. This verifies blocked launches, not that Windows security prompts have been permanently fixed.
- Packaged offline test launchers also call `Assert-PrivateRuntimeFirewall.ps1`. Missing or incomplete exact-path inbound-block rules must stop execution without auto-elevation. The helper only targets private artifact `RuntimeWorld.exe` files, never shared engines or other projects. Policy tests use mocked rules; actual no-prompt engine execution remains unverified.

- Narrow resolved exception: tools/execute-offline-runtime.ps1 may launch only the fixed artifacts/offline-runtime-runner/Windows/RuntimeWorld/Binaries/Win64/RuntimeWorld.exe after verifying its all-profile inbound block and source-copy hashes while holding the exclusive runner lease. It disables trace-server startup and UDP messaging. All other engine launch restrictions remain in force. Never auto-elevate or launch an archive-specific executable.


- tools/execute-packaged-runtime.ps1 is an allowed comparison wrapper only because it delegates all process creation to execute-offline-runtime.ps1. package-runtime-prefab.ps1 and shared editor/build workflows remain blocked.


- User delegation on 2026-10-23 resolves the pending shared-editor approval question: proceed without repeated permission questions within the entrusted scope. Narrow exception: capture-offline-editor-runtime.ps1 may run the installed UE 5.8 UnrealEditor-Cmd.exe against an existing private artifact project, after existing firewall TCP/UDP rules cover every active profile. Trace server, Zen auto-launch and UDP messaging must be disabled. Use an exclusive lease, fresh capture and timeout. No global/shared firewall mutation or automatic elevation. General builds, Unity and RunUAT remain blocked until their own execution paths are validated.


- Audited private build exception after user delegation: resume-offline-ui-api-package.ps1 may resume only artifacts/runtime-ui-api-packaging-v2/Project through installed AutomationTool. Require the same active-profile editor firewall preflight and exclusive engine lease, disable UBA detouring/remote execution, trace-server startup, Zen auto-launch and UDP messaging for cook/pak, monitor disk/time, and never run archived executables directly. Private packaged execution must still use the fixed-path runner.


- Audited Unity exception after user delegation: capture-offline-extended-unity.ps1 may execute only installed Unity 6000.0.83f1 against private artifact fixtures, with existing active-profile firewall coverage, -batchmode -nographics -noUpm, exclusive engine lease, fresh capture validation, disk/time limit and EditorSettings restoration. Never use the user's original project for these tests.


- Unity package-resolution correction: -noUpm prevents required UGUI package references and failed with CS0234. Installed UPM app.js and this exact Unity version's prior logs confirm the -ipc Socket/Pipe transport. capture-offline-extended-unity.ps1 may therefore use UPM only in this named-IPC mode, monitor own UnityPackageManager.exe children for -ipc and no TCP listener, and retain the existing Unity firewall preflight. Do not terminate other projects' package managers. Evidence: artifacts/unity-upm-ipc-source-evidence.json.


- Extended private validation exception: verify-offline-extended-unreal.ps1 may run the installed audited UnrealEditor-Cmd Python commandlet solely on the existing UI API forward fixture and the tracked legacy scene, using the existing firewall preflight and the same no-trace/no-Zen/UDP-disabled options, exclusive lease, fresh outputs and timeout. It must compare real 250-frame captures and cannot compile or run other projects.


- Core private-pipeline exception after user delegation and preflight/ownership tests: .engine-audited-private.json may narrowly resume the C# common runner only under this workspace artifacts directory, only for the two exact already-audited editor paths and the installed UE 5.8 UBT/dotnet pair. Keep the incident marker. Require read-only active-profile coverage before every launch, no-trace/no-Zen/UDP-disabled Unreal options, global local-only compiler flags, owned project paths, exclusive shared test lease, disk monitoring and owned Unity UPM IPC monitoring. Outside this scope remains blocked.


- UE 5.8 clarification from installed ExecutorFactory.cs/UnrealBuildAcceleratorConfig.cs: UBA remains the mandatory local executor. NoUBA disables detouring and consequently remote execution; UBADisableRemote explicitly reinforces no remote listener. Do not claim that the UBA executor itself is removed.

- Explicit renewed user approval on 2026-10-23 at approximately 15:32 KST: "개인 테스트 복사본에서 Unity·Unreal 실행 승인할게. 보안창으로 다른 작업 막지 말고 GC 참조 문제 고쳐서 실제 왕복 테스트까지 계속 진행해줘. 같은 승인 자꾸 묻지 말고." This resolves the automatic-review approval blocker recorded in artifacts/object-equality-check/blocked-audit.json. Resume the already-audited C# common runner for Unity 6000.0.83f1 and Unreal 5.8 / its installed UBT only on owned artifacts copies, including GC reference probes and runtime project roundtrip verification. Keep the incident marker, read-only active-profile firewall preflight before/after, named-IPC-only own Unity UPM monitoring, exclusive shared engine lease, time/disk limits, and Unreal no-trace/no-Zen/no-UDP/local-only compiler options. Do not change global firewall policy, launch archive-specific executables, elevate, use computer-use, or alter original/other projects. Fail closed if coverage is absent; do not repeatedly request the same authorization.
