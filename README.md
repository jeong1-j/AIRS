# AIRS (AI Reliance Regulation System)

AIRS는 AI에게 질문할 때 과도하게 답이나 과제를 맡기는 표현을 살펴보고, 햄스터 Airi가 직접 시도해 볼 한 단계와 질문 개선안을 제안하는 학습 도구입니다.

- [웹 체험판](https://jeong1-j.github.io/AIRS/)
- [Windows용 ZIP 다운로드](https://github.com/jeong1-j/AIRS/releases/latest/download/AIRS-Windows.zip)

## Windows에서 시작하기

1. ZIP 전체를 압축 해제하고 `AIRS.exe`를 실행합니다.
2. **화면 감지 시작**을 눌러 현재 보고 있는 창을 분석합니다. 감지 중에는 작업 표시줄 알림 영역에서 AIRS를 다시 열거나 감지를 중지할 수 있습니다.
3. **햄스터 집**에서 해바라기씨로 간식과 옷을 사용하고, **주간 통계**에서 기록을 확인합니다.

한국어 화면 인식은 Windows에 설치된 한국어 OCR을 사용합니다. 계정과 학생 활동 집계는 실행한 PC에 저장됩니다. 새 PC에서 관리자 기능을 사용하려면 앱의 **로그인 → 첫 관리자 설정**으로 계정을 만듭니다.

## 웹에서 체험하기

**질문 연습**, **햄스터 집**, **주간 통계**를 브라우저에서 이용할 수 있습니다. 판별과 저장은 브라우저에서 이루어집니다. 다른 앱 화면 감지와 로컬 로그인은 Windows 앱에 들어 있습니다.

## 소스

`source/`는 Windows 앱, `dist/`는 정적 웹 체험판입니다. Windows에서 `source/build.ps1`을 실행하면 `AIRS.exe`와 웹 체험판에 사용하는 규칙 파일을 빌드합니다.
