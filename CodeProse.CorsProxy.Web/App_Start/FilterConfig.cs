﻿using System.Web;
using System.Web.Mvc;

namespace CodeProse.CorsProxy.Web
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            //filters.Add(new AllowCrossSiteJsonAttribute());
        }
    }
}