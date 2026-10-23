# 설치 없이 공유하는 웹 체험판

PC 및 모바일 브라우저에 동일한 URL을 배포하고, 개발 중에는 그 주소의 파일을 갱신한다. 정식 Windows 설치판은 별도로 만든다. 모바일은 가로 화면을 기준으로 터치 입력을 제공하며 실제 휴대폰의 성능과 브라우저 호환성은 별도 확인한다.

## 배포 구조

- `main`: 편집 가능한 두 Unity 프로젝트·원화·문서. 저장소의 공개 여부는 배포 스크립트가 변경하지 않는다.
- `gh-pages`: `Tools/Web/stage-site.py`가 선별한 웹 실행 파일·타이틀 원화·라이선스만 포함. 소스나 인증 정보는 복사하지 않는다.
- GitHub Actions의 **Publish web playtest**가 `gh-pages`를 확인한 뒤 Pages에 게시한다. Unity 빌드는 라이선스가 준비된 개발 환경에서 실행하고, GitHub Actions에는 라이선스나 로그인 값을 전달하지 않는다.

웹 실행 파일은 공개 URL에서 다운로드할 수 있다. GitHub Pages는 체험판의 초대 제한이나 비밀번호를 제공하는 구성은 아니다. 2026-10-23 사용자가 Pages를 GitHub Actions로 설정했다고 알려준 뒤 API에서 `has_pages:true`, `private:false`를 확인했다. 현재 소스 저장소도 공개이며, 이 작업의 스크립트가 저장소 공개 여부를 변경한 것은 아니다.

## 최초 Pages 설정

현재 GitHub 연결은 저장소 코드·Actions를 사용할 수 있지만 Pages 관리 API에는 `403 Resource not accessible by integration`을 반환한다. 자동으로 Pages 설정을 바꿨다고 간주하지 않는다. 사용자가 GitHub UI에서 설정한 뒤 활성화를 확인했다.

저장소 소유자가 [Settings → Pages](https://github.com/ka8877/firstgame/settings/pages)에서 **Build and deployment → Source → GitHub Actions**를 선택한다. 비공개 저장소의 Pages는 GitHub 계정 요금제에 따라 제공되지 않을 수 있다. 업그레이드나 소스 공개 전환을 자동으로 수행하지 않는다. 사용할 수 없다면 웹 빌드만 담는 별도 공개 저장소나 사용자가 이미 이용하는 정적 호스팅으로 같은 폴더를 배포할 수 있다.

Pages가 활성화되면 [Publish web playtest](https://github.com/ka8877/firstgame/actions/workflows/publish-web.yml)에서 **Run workflow**를 실행한다. 예상 기본 주소는 `https://ka8877.github.io/firstgame/`이지만, 배포 성공 및 실제 HTTP/게임 실행을 확인하기 전에는 작동하는 주소로 보고하지 않는다.

## 이후 업데이트

두 게임의 수정·native 테스트·Windows/Web 빌드를 마친 뒤 새 Web 파일에 대해 실제 브라우저 검사를 수행한다. 동시 Web 컴파일이나 소프트웨어 브라우저 실행은 피한다.

```bash
python3 Tools/Web/stage-site.py --require-browser-reports
python3 Tools/Web/publish-site.py
gh workflow run publish-web.yml --ref main
```

`stage-site.py`는 성공한 BuildEvidence와 최종 브라우저 보고서의 파일 해시를 대조한다. 동일한 경로의 게임 페이지를 유지하고 Unity 로더·데이터·WASM은 내용 해시 파일명을 사용해 업데이트 때 오래된 실행 파일이 섞이지 않도록 한다. 공개용 이미지·모델은 일반 Git 파일이며 Pages에 Git LFS 포인터를 올리지 않는다. 소스 저장소의 Git LFS 정책은 유지한다.

`build-manifest.json`에 배포 파일 SHA-256·소스 커밋·생성 시각을 남긴다. 게시 후 랜딩·두 게임 HTML·실제 로더/데이터/WASM·브라우저 입력·저장/새로고침을 확인한다. 실패하면 최신 배포를 성공이라고 보고하지 않으며 기존 빌드 ZIP을 대안으로 제공한다.

브라우저 저장은 주소·브라우저별로 분리된다. 배포 주소를 바꾸거나 브라우저 데이터를 지우기 전에는 게임의 저장 내보내기를 사용한다. 두 게임의 core ID와 저장 스키마는 이번 아트 업데이트에서 바꾸지 않는다.
