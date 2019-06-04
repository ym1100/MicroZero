using System;
using System.Text;
using Agebull.Common.Context;
using Agebull.Common.Ioc;
using Agebull.Common.Logging;
using Agebull.Common.OAuth;
using Agebull.MicroZero;
using Agebull.MicroZero.Helpers;
using Agebull.MicroZero.ZeroApis;
using Microsoft.AspNetCore.Http;

namespace MicroZero.Http.Gateway
{
    /// <summary>
    ///     调用映射核心类
    /// </summary>
    internal partial class Router : IRouter
    {
        #region 变量

        /// <summary>
        ///     Http上下文
        /// </summary>
        public HttpContext HttpContext { get; set; }

        /// <summary>
        ///     Http请求
        /// </summary>
        public HttpRequest Request { get; set; }

        /// <summary>
        ///     Http返回
        /// </summary>
        public HttpResponse Response { get; set; }

        /// <summary>
        ///     Http返回
        /// </summary>
        public RouteData Data { get; set; }

        /// <summary>
        ///     安全检查器
        /// </summary>
        public SecurityChecker SecurityChecker { get; set; }

        #endregion

        #region 流程

        /// <summary>
        ///     调用
        /// </summary>
        /// <param name="context"></param>
        bool IRouter.Prepare(HttpContext context)
        {
            LogRecorderX.MonitorTrace("ApiRouter");
            HttpContext = context;
            Request = context.Request;
            Response = context.Response;
            Data = new RouteData();
            var success = Data.Prepare(HttpContext);
            IoHandler.OnBegin(Data);
            if (!success)
            {
                Data.UserState = UserOperatorStateType.LogicalError;
                Data.ZeroState = ZeroOperatorStateType.ArgumentInvalid;
                Data.ResultMessage = ApiResultIoc.ArgumentErrorJson;
                return false;
            }
            SecurityChecker = new SecurityChecker { Data = Data };
            return SecurityChecker.PreCheck();
        }

        /// <summary>
        ///     调用
        /// </summary>
        async void IRouter.Call()
        {
            if (Data.ApiHost.Equals("Zero", StringComparison.OrdinalIgnoreCase))
            {
                var manager = new ZeroManager();
                manager.Command(Data);
                return;
            }

            // 1 初始化路由信息
            if (!FindHost())
            {
                Data.UserState = UserOperatorStateType.NotFind;
                Data.ZeroState = ZeroOperatorStateType.NotFind;
                Data.ResultMessage = ApiResultIoc.NoFindJson;

                return;
            }

            // 2 安全检查
            if (!TokenCheck())
            {
                Data.UserState = UserOperatorStateType.DenyAccess;
                Data.ZeroState = ZeroOperatorStateType.DenyAccess;
                if (Data.ResultMessage == null)
                    Data.ResultMessage = ApiResultIoc.DenyAccessJson;
                return;
            }
            // 3 缓存快速处理
            if (RouteCache.LoadCache(Data.Uri, Data.Token, out Data.CacheSetting, out Data.CacheKey, ref Data.ResultMessage))
            {
                //找到并返回缓存
                Data.UserState = UserOperatorStateType.Success;
                Data.ZeroState = ZeroOperatorStateType.Ok;
                return;
            }

            // 4 远程调用
            if (!Data.RouteHost.ByZero)
                Data.ResultMessage = await CallHttp();
            else if (ZeroApplication.ZerCenterIsRun)
                Data.ResultMessage = CallZero();
            else
            {
                Data.UserState = UserOperatorStateType.LocalError;
                Data.ZeroState = ZeroOperatorStateType.NotFind;
                Data.ResultMessage = ApiResultIoc.NoReadyJson;
                return;
            }
            // 5 结果检查
            if (RouteOption.Option.SystemConfig.CheckResult)
                SecurityChecker.CheckResult(Data);
        }


        /// <summary>
        ///     查找站点
        /// </summary>
        private bool FindHost()
        {
            if (!RouteOption.RouteMap.TryGetValue(Data.ApiHost, out Data.RouteHost) || Data.RouteHost == null)
            {
                LogRecorderX.MonitorTrace($"{Data.ApiHost} no find");
                return false; //Data.RouteHost = HttpHost.DefaultHost;
            }
            //if(Data.RouteHost.Failed)
            //{
            //    LogRecorderX.MonitorTrace($"{Data.HostName} is failed({Data.RouteHost.Description})");
            //    return false; //Data.RouteHost = HttpHost.DefaultHost;
            //}
            if (!RouteOption.Option.SystemConfig.CheckApiItem || !(Data.RouteHost is ZeroHost host))
                return true;
            if (host.Apis == null || !host.Apis.TryGetValue(Data.ApiName, out Data.ApiItem))
            {
                LogRecorderX.MonitorTrace($"{Data.ApiHost}/{Data.ApiName} no find");
                return false; //Data.RouteHost = HttpHost.DefaultHost;
            }
            if (Data.ApiItem.Access == ApiAccessOption.None || Data.ApiItem.Access.HasFlag(ApiAccessOption.Public))
                return true;
            LogRecorderX.MonitorTrace($"{Data.ApiHost}/{Data.ApiName} deny access.");
            return false;
        }

        /// <summary>
        ///     安全检查
        /// </summary>
        private bool TokenCheck()
        {

            SecurityChecker.Data = Data;
            //if (SecurityChecker.CheckToken())
            //    return true;
            //Data.ResultMessage = RouteOption.Option.Security.BlockHost;
            //Response.Redirect(RouteOption.Option.Security.BlockHost, false);
            //Data.Redirect = true;
            return RouteOption.Option.Security.Auth2
                ? SecurityChecker.CheckToken2()
                : SecurityChecker.CheckToken();
        }


        /// <summary>
        ///     写入返回
        /// </summary>
        void IRouter.WriteResult()
        {
            //if (Data.Redirect)
            //    return;
            //// 缓存
            //RouteCache.CacheResult(Data);
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            Response.WriteAsync(Data.ResultMessage ?? (Data.ResultMessage = ApiResultIoc.RemoteEmptyErrorJson), Encoding.UTF8);
        }

        /// <summary>
        ///     结束
        /// </summary>
        void IRouter.End()
        {
            IoHandler.OnEnd(Data);
        }

        /// <summary>
        /// 异常处理
        /// </summary>
        /// <param name="e"></param>
        /// <param name="context"></param>
        void IRouter.OnError(Exception e, HttpContext context)
        {
            try
            {
                LogRecorderX.MonitorTrace(e.Message);
                Data.UserState = UserOperatorStateType.LocalException;
                Data.ZeroState = ZeroOperatorStateType.LocalException;
                ZeroTrace.WriteException("Route", e);
                IocHelper.Create<IRuntimeWaring>()?.Waring("Route", Data.Uri.LocalPath, e.Message);
                context.Response.WriteAsync(ApiResultIoc.LocalErrorJson, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                LogRecorderX.Exception(exception);
            }
        }

        #endregion
    }
}