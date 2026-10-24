# EngineMigrator

Windows 설치형 Unity ↔ Unreal 프로젝트 포팅 도구의 개발 프리뷰입니다. **전체 게임·모든 코드·모든 엔진 버전의 자동 포팅은 아직 미완료입니다.** 지원하지 않는 항목은 차단 사유로 남기며 원본 프로젝트는 수정하지 않습니다.

## 현재 동작

- 프로젝트·버전·경로·파일·C# 구문 조사와 HTML/JSON 보고서
- 독립 Source/Target 복사본에서 실제 엔진을 실행하는 장면·리소스 포팅
- PNG/JPEG/TGA, 기본 URP Lit/Unlit 및 Standard/Unlit, 단일 삼각형 서브메시, 계층·좌표·UV·법선·공유 참조
- Read/Write가 꺼진 임포트 FBX 메시의 에디터 추출과 렌더러 활성·그림자 상태 보존
- 카메라·방향광·Global Volume의 일부 후처리·환경 매핑. 렌더링 동등성은 미검증이며 엔진 고유 값은 왕복 메타데이터에 보존
- 장면 MonoBehaviour를 Runtime Actor 플러그인으로 컴파일하고 원래 객체에 부착
- float/double/bool/int/GameObject/Vector3, private/protected SerializeField, 인스턴스 값·객체/self/null 참조
- Awake/Start/Update/OnDestroy, 단일 상속·virtual/override·명시적인 부모 호출, 기본 수식·분기·while
- 정의된 int 래핑 연산과 증감, 자신의 Transform 월드/로컬 위치 대입·Vector3 기본 연산
- 실제 생성 헤더에서 C#을 복원해 Unity 장면에 컴포넌트·직렬화·참조 재연결
- 별도의 제한된 정적 C#↔C++ 함수와 실제 Blueprint Function Library 변환
- 엔진 BuildId별 네이티브 모듈 선택. 다른 빌드의 DLL은 로드하지 않음
- 제한된 Actor Blueprint 이벤트 그래프의 실제 노드 생성·역추출: 숫자/bool 상태, 대입·분기, BeginPlay/Tick/EndPlay, 부모 호출·상속과 인수 없는 공개 Custom Event
- Box/Sphere/Capsule Collider·Rigidbody의 실제 ShapeComponent/Chaos 강체, 질량·중력·감쇠·축 잠금·트리거·공유 PhysicalMaterial
- Legacy Animation 위치·크기 곡선 ↔ 실제 LevelSequence 채널, 키·접선·반복·대상 참조
- Overlay uGUI ↔ 실제 Widget Blueprint, RectTransform 앵커·피벗·오프셋·색·텍스트와 순서/비활성 상태를 보존하는 Button.onClick 런타임 연결
- 런타임 UI 프리팹의 위젯 생성·인스턴스별 콜백 대상 재연결·비활성화·삭제·장면 전환 정리. UI Transform 화면 좌표·배율과 일반 UI 컴포넌트 API, 씬 배치 UI의 동적 복제는 추가 구현·검증 대상
- 설치 해시가 일치하는 자체 Unity 어댑터 갱신 및 사용자 수정 파일 보호

설명과 제한: [오프라인 HTML 가이드](guide.html). 결과 폴더의 `resource-port.html`과 `Transfer/*-import-result.json`에서 원본 ID→대상 객체·리소스→참조를 확인하세요.

## 실행

배포 폴더의 `EngineMigrator.App.exe`를 실행하세요. 개발 환경에서는 .NET 9 SDK와 Windows Desktop 런타임이 필요합니다.

```powershell
dotnet run --project src/EngineMigrator.App
```

프로젝트 검사 후 **장면·리소스 포팅**을 사용하면 장면에 붙은 지원 스크립트의 컴파일·부착·상태·참조 이전도 함께 수행합니다. 클래스 변환 메뉴는 클래스만 따로 생성합니다. 원본과 대상 엔진의 정확한 버전이 설치되어 있어야 하며 자동 업그레이드하지 않습니다. C++ 생성에는 Unreal의 빌드 도구와 호환되는 Visual Studio C++ 환경이 필요합니다.

```powershell
dotnet build src/EngineMigrator.Cli -c Release
dotnet src/EngineMigrator.Cli/bin/Release/net9.0/EngineMigrator.Cli.dll resources Unity SOURCE TARGET OUTPUT UNITY_EXE UNREAL_EXE ADAPTERS_DIR
```

Unity는 활성 빌드 장면 하나, Unreal은 `/Game/` 시작 맵이 필요합니다. 선택한 원본·대상은 보존하고 OUTPUT 아래의 복사본에서 작업합니다. 캐시도 독립된 파일로 복사합니다. 심볼릭 링크·정션은 거부합니다.

## 검증

2026-10-19 실제 실행 환경: Unity **6000.0.83f1**, Unreal **5.8.1 / 5.5.4**. 사용자 `EngineMigratorUnity`의 기본 URP 장면과, 원본을 보존한 복사본에 추가한 검증용 로직을 사용했습니다.

확장 검증: UE5.8.1 / UE5.5.4와 Unity의 18개 객체 실제 왕복 데이터 대조 및 원본·Unreal·복원 Unity의 250프레임 실행 비교가 통과했습니다. Actor 3개, Transform 애니메이션 1개, 동적 강체 2개와 UMG 트리를 확인했고, 두 번의 버튼 콜백 결과는 모두 1212였습니다. 물리 자유낙하는 약 0.000002m 이내이나 접촉 회복 중 최대 약 0.20m 차이가 있어 전체 물리 궤적 동등성은 **false**입니다. 렌더링·마우스 입력·패키징 동등성은 미검증입니다. Animator·스켈레탈·임의 Blueprint/API·복잡한 UI는 미완료입니다.

- 원래 카메라·조명·후처리·환경의 양방향 저장·재열기·재추출 대조
- 16개 객체, 텍스처 3개, 머티리얼 3개, 메시 3개의 픽셀·면 방향·UV·법선·설정·공유 참조 왕복 대조
- 6개 컴포넌트의 상속·정수 오버플로·객체 참조·Vector3·월드/로컬 이동을 양쪽 엔진에서 네 프레임 실행 비교
- private 직렬화 필드 포함 7개 컴포넌트와 별도의 EndPlay 종료 경로 실행 비교
- 복원된 Unity 이동 장면의 실제 실행 비교
- 두 Unreal 버전의 실제 Blueprint 생성·저장·재열기·실행 네 사례 비교
- 실제 FBX Read/Write 비활성 상태에서 24개 정점·12개 삼각형과 UV·법선 추출 비교

최신 상세 결과·경로는 개발 세션 보고서와 각 작업의 검증 JSON에 기록합니다. 저장/참조 검증은 일반 게임플레이·렌더링·패키징 검증을 대체하지 않습니다. `projectPortComplete`는 false입니다.

```powershell
dotnet run --project tests/EngineMigrator.Checks -c Release
cmd /c tools\verify-native.cmd
cmd /c tools\verify-script-loops.cmd
dotnet run --project src/EngineMigrator.App -- --smoke-test
./tools/verify-resource-roundtrip.ps1
./tools/verify-scene-behaviours.ps1 -Run PRIVATE_FORWARD_RUN
./tools/verify-scene-behaviours.ps1 -Run PRIVATE_FORWARD_RUN -EndPlayOnly
./tools/verify-scene-behaviours.ps1 -Run PRIVATE_FORWARD_RUN -UnityProject RESTORED_UNITY_COPY
```

엔진 검증 도구는 실제 에디터를 실행합니다. `verify-scene-behaviours.ps1`은 원본이 아닌 artifacts 아래의 검증용 복사본만 수정합니다. 사용자 프로젝트에서 임의로 실행하는 일반 게임 검증기는 아닙니다.

네이티브 Blueprint 브리지는 선택한 엔진마다 빌드합니다.

```powershell
./tools/build-blueprint-bridge.ps1 -EngineRoot 'C:/Program Files/Epic Games/UE_5.5/Engine'
```

엔진 BuildId별로 `artifacts/blueprint-versions/BUILD_ID/EMBlueprintBridge`에 저장하고 앱 빌드에 포함합니다. 엔진 버전 문자열만 읽거나 다른 버전 DLL을 재사용한 것을 지원 검증으로 표시하지 않습니다.

## 남은 범위

C++ Runtime Actor의 단일 장면 FixedUpdate는 100/50/30 FPS와 0.02/0.01초 간격의 실제 왕복 대조를 통과했습니다. 프로젝트 고정 간격을 전달하며 상속·활성 전환·동적 컴포넌트를 처리합니다. 고정 시계의 3장면 전환과 빈 중간 장면도 실제 왕복 대조를 통과했습니다. 물리 서브스텝과 임의 가변 프레임은 별도 검증 범위입니다.


일반 Actor Blueprint 이벤트 그래프·전체 엔진 API·외부 DLL/패키지, Actor Blueprint의 FixedUpdate/LateUpdate·OnEnable/OnDisable·코루틴, 비활성 컴포넌트 생명주기, 물리·애니메이션·스켈레탈 메시·UI·입력·오디오·내비게이션, 중첩·Variant 프리팹·런타임 장면 전환·다중 서브메시·LOD·복잡한 셰이더 및 모든 버전 조합은 미완료입니다. 렌더링 외형 일치와 모든 플랫폼 배포는 미검증입니다. Windows Development 패키지는 별도 검증용 복사본에서 실제 실행해 250프레임의 상태·애니메이션·버튼 delegate·기본 물리를 대조했습니다. Unity에서 기록한 동일 deltaTime 입력을 사용한 검사이며 자연 FPS·실제 마우스 입력의 동등성은 검증하지 않았습니다.

로컬 LLM은 선택 기능으로 예정되어 있으며 현재 연결되지 않습니다. 모델 응답만으로 변환 성공을 판정하지 않습니다.

## Windows 게임 실행과 UI 이미지 확장

게임 모듈과 Game/Editor 타깃을 생성해 Runtime 플러그인을 실제 패키지에 포함합니다. 맵 전용 GameMode는 원본에 없는 Pawn·Spectator·HUD 자동 생성을 막습니다. 저장 후 재열어 설정을 검사하며, 사용자 수정 호스트 코드와 모듈 구성이 있으면 자동 역변환을 차단합니다.

기본 PNG UI 텍스처, Simple Sprite 사각 영역·피벗·PPU·공유 참조, RawImage UV와 버튼 자식 계층을 변환합니다. 패킹·트리밍·슬라이스·별도 overrideSprite 상태·알파 히트 테스트·TMP·자동 레이아웃은 지원하지 않습니다. 실제 픽셀·참조 검사와 렌더링 외형 검사는 구분합니다.

## 프로젝트 범위 사전 검사와 왕복

장면이 여러 개이거나 프리팹이 있으면 프로젝트 단위로 실행합니다. 같은 장면명은 GUID로 구분하고 빌드 순서·활성 플래그를 보존합니다. 메시·재질·텍스처를 장면 간 공유하며, 동일 ID의 실제 데이터 충돌을 차단합니다. GUID·fileID·컴포넌트·지원 스크립트는 대상 자산 생성 전에 검사합니다.

프리팹 정의는 Unreal 맵 자산으로 보존하고 배치 객체는 펼칩니다. Unity 복원은 실제 Prefab Asset/Instance와 속성 오버라이드·GameObject 참조를 복원합니다. Unreal 정의 수정 자동 전파, 중첩/Variant, 구조 변경, 런타임 생성·장면 전환은 미지원입니다. 임의 Unreal 프로젝트 전체 맵 탐색은 아직 지원하지 않으며, 추적된 변환 결과를 프로젝트 단위로 왕복합니다.

메시 OBJ handedness를 보정하고 정점·UV·노멀·삼각형 방향을 대조합니다. 원본 MeshDescription의 노멀을 읽는 네이티브 모듈을 포함합니다.

C++ Runtime Actor의 실행 중 Time.timeScale 단순 대입·읽기는 0.5배속·정지·2배속·정상 복귀와 음수 무시를 100/30 FPS의 두 스크립트 실제 왕복으로 대조했습니다. 초기 0·0.5·1과 배율 변경 중 동적 객체·장면 전환도 실제 왕복 대조를 통과했습니다. 프리팹 자산 생성·물리 전체 동등성은 추가 검증 범위입니다.

활성·비활성 일반 프리팹 두 자산은 초기 0/0.5배속과 실행 중 정지·2배속에서 실제 스테이징·참조 내보내기·Unity 프로젝트 가져오기 및 런타임 왕복 대조를 통과했습니다. 물리·애니메이션·UI와 중첩 프리팹의 해당 조건 동등성은 추가 검증 범위입니다.

C++ Runtime Actor의 LateUpdate는 모든 일반 업데이트 뒤의 실행 단계로 변환하며, 상속·재활성·동적 추가·정지·삭제를 100/30 FPS 실제 왕복으로 확인했습니다. 후기 이벤트 내부의 모든 추가/전환 조합과 다른 시스템의 교차 동등성은 추가 검증 범위입니다.

후기 생명주기 런타임 버전 2는 LateUpdate 안에서 요청한 생성·복제·재활성·삭제 및 세 장면 전환을 100/30 FPS 실제 왕복으로 확인했습니다. 기존 후기 헤더의 역변환을 유지하며 서로 다른 실행 버전의 혼합은 거부합니다.

후기 런타임 버전 3은 LateUpdate를 포함한 묶음의 Start·OnEnable 중첩 추가, 같은 후기 이벤트의 활성 토글, 동적 스크립트 실행 순서를 100/30 FPS 실제 왕복으로 검증했습니다. 후기 이벤트 없는 묶음의 시작 대기열은 추가 검증 범위입니다.

프로젝트 통합 검증은 기존 49개와 후기 생명주기 4개 클래스를 함께 실제 양방향 프로젝트 경로로 변환하여, 20장면·25프리팹과 런타임 상태·참조·GC·복원 재내보내기를 대조했습니다. 계산된 int 장면 인덱스의 프로젝트 사전 검사와 빈 첫 단위의 병합 스크립트 복원 누락을 수정했습니다. 임의 프로젝트·모든 버전 완료를 의미하지 않습니다.

LateUpdate 없는 묶음의 Start/OnEnable 중첩 생성은 100/30 FPS 실제 왕복과 별도 49개 스크립트 프로젝트로 검증했습니다. 프레임 끝 시작과 고정 시간별 시작 단계를 보강했습니다. 동일 클래스 인스턴스의 미정의 Update 순서는 보장하지 않습니다.

FixedUpdate 중 생성·복제·활성 변경의 시작 경계를 형식 5로 보강했습니다. 네 조건의 실제 왕복과 55개 클래스 프로젝트 파이프라인·저장 자산 재내보내기를 검증했으며, 이전 형식 1~4의 역변환을 유지합니다.

