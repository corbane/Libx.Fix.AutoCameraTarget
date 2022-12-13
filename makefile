# https://developer.rhino3d.com/guides/yak/pushing-a-package-to-the-server/


test:
	"C:\Program Files\Rhino 7\System\Yak.exe" search --all autocameratarget

push:
	cd yak/1.2.0 && \
	"C:\Program Files\Rhino 7\System\Yak.exe" push autocameratarget-1.2.0-rh7_24-any.yak
