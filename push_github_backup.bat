@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo === Git backup: Abyssdawn ===
git rev-parse --is-inside-work-tree 2>nul | findstr /i true >nul
if errorlevel 1 (
  echo [오류] Git 저장소가 아닙니다.
  exit /b 1
)

echo.
echo [1/4] git add -A (.gitignore 제외)
git add -A
if errorlevel 1 goto :err

echo.
echo [2/4] 상태
git status --short

echo.
echo [3/4] 커밋
git commit -m "chore: backup — workspace sync"
if errorlevel 1 (
  echo 커밋이 생략되었을 수 있습니다 ^(변경 없음 또는 이미 스테이징됨^).
)

echo.
echo [4/4] GitHub로 push ^(origin 현재 브랜치^)
for /f "delims=" %%b in ('git branch --show-current') do set "BR=%%b"
if "%BR%"=="" set "BR=main"
git push origin "%BR%"
if errorlevel 1 goto :err

echo.
echo 완료: origin/%BR%
exit /b 0

:err
echo.
echo [실패] 위 메시지를 확인하세요. 인증이 필요하면 Git Credential Manager 또는 PAT를 설정하세요.
exit /b 1
