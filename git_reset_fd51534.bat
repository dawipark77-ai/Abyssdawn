@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo.
echo === Git reset to fd51534 ===
echo Unity 에디터를 먼저 완전히 종료하세요. (열어두면 Logs 아래 .log 가 잠겨 reset 이 멈춥니다)
echo.
pause

REM 셰이더 컴파일러만 남아 잠그는 경우가 많음 — 에디터 종료 뒤에만 안전
taskkill /IM UnityShaderCompiler.exe /F >nul 2>&1

if exist "Logs" (
  attrib -r "Logs\*.*" /s >nul 2>&1
  del /f /q "Logs\shadercompiler-UnityShaderCompiler.exe-0.log" >nul 2>&1
  del /f /q "Logs\*.log" >nul 2>&1
)

echo.
echo Resetting to fd51534...
git reset --hard fd51534c5005985809f975368616046622a03748
if errorlevel 1 (
  echo.
  echo [실패] 여전히 파일이 잠겼을 수 있습니다.
  echo  - Unity / Hub / Rider 가 Logs 를 쓰는지 확인 후 다시 실행하세요.
  echo  - 또는 수동: tasklist ^| findstr /i unity
  pause
  exit /b 1
)

git log -1 --oneline
echo.
echo Done.
pause
