# 테스트 산출물 정리

변환 실행 종료 시 전용 artifacts 작업 폴더의 Unreal Intermediate를 자동 삭제합니다. 실패·취소도 포함합니다. 소스·변환 결과·로그·Binaries·패키지·Unity PackageCache는 보존합니다. 기존 실행 파일에는 새 코드가 반영되지 않으므로 재빌드 후 적용됩니다.

기존 캐시 정리: `python tools/cleanup-build-cache.py --apply`

외부 테스트 명령 종료 후 정리: `python tools/cleanup-build-cache.py --run <명령> <인수>`

인수 없이 실행하면 용량만 확인합니다. 엔진 공용 잠금이 사용 중이면 삭제하지 않습니다. 링크 경로를 거부하고 삭제 결과를 artifacts/cache-cleanup-날짜.json에 기록합니다. 빌드 캐시는 다음 실행에서 재생성됩니다.

Unity Library의 재생성 가능한 하위 캐시도 정리하며 PackageCache·PackageManager는 보존합니다. Library 루트 파일과 검증 결과는 유지합니다.

전용 빌드 bin/Binaries의 재생성 가능한 PDB 심볼도 정리합니다. 기존 고정 런타임 러너는 일괄 정리에서 제외합니다.
