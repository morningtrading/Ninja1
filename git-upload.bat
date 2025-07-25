@echo off
git add .
git commit -m "Auto update"
git pull origin main --rebase
git push origin main
pause