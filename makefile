# https://developer.rhino3d.com/guides/yak/pushing-a-package-to-the-server/

VERSION=1.3.0

test:
	"C:\Program Files\Rhino 7\System\Yak.exe" search --all autocameratarget

push:
	cd yak/$(VERSION) && \
	"C:\Program Files\Rhino 7\System\Yak.exe" push autocameratarget-$(VERSION)-rh7_24-any.yak
