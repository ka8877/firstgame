# 현대 UI 글꼴

두 게임의 현재 UI는 **Pretendard 1.3.9 Regular / SemiBold**를 사용한다. 원본 폰트 프로그램은 [공식 릴리스 태그](https://github.com/orioncactus/pretendard/tree/v1.3.9)의 OTF를 수정 없이 포함했다. Pretendard는 Kil Hyung-jin이 제작했으며 SIL Open Font License 1.1로 제공한다. 다운로드 URL·바이트 수·SHA-256과 문자 커버리지는 [ModernTypography.json](ModernTypography.json)에 기록했다.

- `Resources/Fonts/Korean`: 본문·성경·수치용 Regular. 기존 `Korean.asset`과 `.meta` GUID를 유지하며 Prepare에서 소스·메트릭·글리프를 갱신한다. 기존 재질과 atlas subasset도 교체하지 않고 재사용한다.
- `Resources/Fonts/Display`: 제목·버튼·강조용 SemiBold. 기존 Bold / 600 / 700 텍스트도 Regular의 weight table을 통해 실제 SemiBold 서체를 사용한다.
- 이전 `FirstGameKorean.otf`와 기존 라이선스 본문은 보존한다. 새 원본 라이선스는 `Fonts/Pretendard-OFL.txt`에 있다. 기존 배포 도구가 복사하는 `Fonts/OFL.txt`에도 Pretendard의 저작권·라이선스를 덧붙였으므로 Windows/Web의 ThirdParty 안내에 함께 포함된다.

`ProjectSetup.CreateFont()`는 현재 `Assets/Game`의 C#·Ink 문자열과 기본 라틴·기호에서 문자 집합을 모아 두 weight 모두 채운다. 48px SDF, 2048² atlas, padding 6과 동적 다중 atlas를 사용한다. 원본 한글 전체를 미리 픽셀로 채워 빌드를 크게 만들지 않으며, 각 폰트 원본에는 한글 11,172자를 포함한 14,336개 Unicode cmap 항목이 있다. 새 UI의 12px 최소 표시 기준은 레이아웃에서 유지한다.

현재 파일의 cmap 검사에서 두 게임 소스의 한글·구절·기호 누락은 0이었다. 이는 원본 폰트 커버리지 검사이며 Unity import나 실제 렌더링 통과와는 다르다. 실제 Prepare와 빌드 후 제목·본문·모바일 카드 수치를 확인한다. 기존 Korean GUID를 바꾸지 않는 migration은 고정된 Unity 6000.3 TMP Editor source setter로 두 소스 참조와 source GUID를 함께 갱신한다. Prepare를 다시 실행하면 소스가 같을 때 atlas를 초기화하지 않는다.

라이선스의 저작권·조항은 그대로 보존하고, 배포용 텍스트에서는 줄 끝 공백만 정리했다. 원본과 배포용 라이선스의 SHA-256을 각각 기록했다.
