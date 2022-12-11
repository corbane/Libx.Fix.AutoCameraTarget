# https://developer.rhino3d.com/guides/yak/pushing-a-package-to-the-server/

build:
	cd bin/Release/net48/ && \
	"C:\Program Files\Rhino 7\System\Yak.exe" build  && \
	mv libx.fix.autocameratarget-1.0.0-rh7_24-any.yak autocameratarget-1.0.0-rh7_24-any.yak

init:
	cd bin/Release/net48/ && \
	"C:\Program Files\Rhino 7\System\Yak.exe" spec

push:
	cd bin/Release/net48/ && \
	"C:\Program Files\Rhino 7\System\Yak.exe" push autocameratarget-1.0.0-rh7_24-any.yak

test:
	"C:\Program Files\Rhino 7\System\Yak.exe" search --all autocameratarget
