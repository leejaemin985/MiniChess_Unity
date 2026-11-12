# WebGL PWA

`Assets/WebGLTemplates/MiniChessPWA`가 프로젝트의 WebGL 템플릿이다. 기존 `MiniChess/Build/WebGL (GitHub Pages)` 메뉴로 빌드하면 PWA 파일도 `Builds/WebGL`에 생성된다.

- Manifest의 `display: standalone`으로 설치 후 실행할 때 주소창과 브라우저 툴바를 숨긴다. 일반 브라우저 탭에서는 브라우저 UI가 유지된다.
- Android에서는 브라우저 메뉴의 앱 설치/홈 화면 추가를 사용한다. iPhone/iPad에서는 Safari의 공유 메뉴에서 홈 화면에 추가하고 해당 아이콘으로 실행한다.
- 기기 상태바와 홈 제스처 영역은 OS 정책에 따른다. 게임은 safe-area 여백을 두어 노치와 홈 표시기 아래에 UI가 겹치지 않게 한다.
- 이름은 MiniChess이며 192/512px 앱 아이콘과 180px Apple 홈 화면 아이콘을 제공한다.
- Manifest ID, 시작 URL, scope와 서비스 워커 경로는 상대 경로다. GitHub Pages의 `/MiniChess_Unity/` 경로 안에서 동작한다.
- 서비스 워커는 네트워크 요청을 전달하며 게임 데이터나 HTML을 Cache Storage에 저장하지 않는다. 오프라인 플레이는 제공하지 않는다. HTTP 캐시는 호스팅 서버의 헤더를 따른다.
- 배포 시 `Build`, `TemplateData`, `Icons`, `index.html`, `manifest.webmanifest`, `pwa.js`, `service-worker.js`를 함께 복사한다. Unity `.meta` 파일은 배포하지 않는다.

참고: [MDN 설치 조건](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Making_PWAs_installable), [WebKit 홈 화면 웹 앱](https://webkit.org/blog/13878/).
