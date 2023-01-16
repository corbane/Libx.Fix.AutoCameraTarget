/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/

#define WIN32


using System;
using System.Linq;
using System.Diagnostics;
using System.Reflection;

using RhinoApp = Rhino.RhinoApp;


#if DEBUG
using WSS = WebSocketSharp.Server;
#endif


#if RHP
namespace Libx.Fix.AutoCameraTarget;
#endif


static class DBG
{
    interface IDbgService
    {
        void Emit (string message);
        void Print (string message);
    }

    class RService : IDbgService
    {
        public void Emit (string message) { RhinoApp.WriteLine (message); }
        public void Print (string message) { RhinoApp.WriteLine (message); }
    }

    static IDbgService _service = new RService ();

    #if DEBUG
    static WSS.WebSocketServer _server = new ("ws://localhost:8080");
    class WService : WSS.WebSocketBehavior, IDbgService
    {
        public void Emit (string message) { Send (message); }
        public void Print (string message) { RhinoApp.WriteLine (message); Send (message); }
    }
    #endif
    
    public static void Start ()
    {
        #if DEBUG
        _server.AddWebSocketService <WService> ("/", delegate {
            var srv = new WService();
            _service = srv;
            return srv;
        });
        _server.Start ();
        #endif
    }
    
    public static void Stop ()
    {
        #if DEBUG
        _server.Stop ();
        #endif
    }


    static void _Emit (string group, MethodBase mT, object? message = null)
    {
        _service.Emit ($"[{group} {mT.DeclaringType.Name}.{mT.Name}] {message}");
    }

    static void _Print (string group, MethodBase mT, object? message = null)
    {
        _service.Print ($"[{group} {mT.DeclaringType.Name}.{mT.Name}] {message}");
    }


    [Conditional("DEBUG_EVENT")]
    public static void EVENT (params object?[] messages)
    {
        _Emit ("EVEN",
            new StackTrace().GetFrame (1).GetMethod(),
            string.Join (" ", from o in messages select o == null ? "null" : ""+o)
        );
    }


    [Conditional("DEBUG_DATA")]
    public static void DATA (params object?[] messages)
    {
        _Emit ("DATA",
            new StackTrace().GetFrame (1).GetMethod(),
            string.Join (" ", from o in messages select o == null ? "null" : ""+o)
        );
    }

    [Conditional("DEBUG_PROP")]
    public static void PROP (params object?[] messages)
    {
        _Emit ("PROP",
            new StackTrace().GetFrame (1).GetMethod(),
            string.Join (" ", from o in messages select o == null ? "null" : ""+o)
        );
    }

    [Conditional("DEBUG_CTOR")]
    public static void CTOR (params object?[] messages)
    {
        _Print ("CTOR",
            new StackTrace().GetFrame (1).GetMethod(),
            string.Join (" ", from o in messages select o == null ? "null" : ""+o)
        );
    }

    [Conditional("DEBUG")]
    public static void Log (params object?[] messages)
    {
        _Emit ("",
            new StackTrace().GetFrame (1).GetMethod(),
            string.Join (" ", from o in messages select o == null ? "null" : ""+o)
        );
    }

    public static void Fail (params object[] messages)
    {
        _Print ("!!! ERROR",
            new StackTrace().GetFrame (1).GetMethod(),
            string.Join (" ", from o in messages select ""+o)
        );
    }

}
