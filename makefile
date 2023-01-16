# https://developer.rhino3d.com/guides/yak/pushing-a-package-to-the-server/

VERSION=2.2.0

test:
	"C:\Program Files\Rhino 7\System\Yak.exe" search --all autocameratarget

push:
	cd yak/$(VERSION) && \
	"C:\Program Files\Rhino 7\System\Yak.exe" push autocameratarget-$(VERSION)-rh7_25-win.yak

DELETE:
	DEL /Q /F "%APPDATA%\McNeel\Rhinoceros\7.0\UI\Plug-ins\Libx.Fix.AutoCameraTarget.rui"
	DEL /Q /F "%APPDATA%\McNeel\Rhinoceros\7.0\UI\Plug-ins\Libx.Fix.AutoCameraTarget.rui.rui_bak"
	REG DELETE "HKEY_USERS\S-1-5-21-778555592-213829105-1058500975-1001\Software\McNeel\Rhinoceros\7.0\Plug-Ins\45d93b79-52d5-4ee8-bfba-ee4816bf0080"  /f
	