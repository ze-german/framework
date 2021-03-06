﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Signum.Engine;
using Signum.Entities.Basics;
using Signum.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Signum.React.Filters
{
    public class SignumAuthenticationResult
    {
        public IUserEntity User { get; set; }
    }

    public class SignumEnableBufferingFilter : IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            context.HttpContext.Request.EnableBuffering();
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
        }

    }

    public class CleanThreadContextAndAssertFilter : IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
            Statics.CleanThreadContextAndAssert();
        }
    }

    public class SignumAuthenticationFilter : SignumDisposableResourceFilter
    {
        public SignumAuthenticationFilter() : base("Signum_User"){ }

        public static readonly IList<Func<FilterContext, SignumAuthenticationResult>> Authenticators = new List<Func<FilterContext, SignumAuthenticationResult>>();

        private static SignumAuthenticationResult Authenticate(ResourceExecutingContext actionContext)
        {
            foreach (var item in Authenticators)
            {
                var result = item(actionContext);
                if (result != null)
                    return result;
            }

            return null;
        }

        public override IDisposable GetResource(ResourceExecutingContext context)
        {
            var result = Authenticate(context);

            if (result == null)
                return null;            

            return result.User != null ? UserHolder.UserSession(result.User) : null;
        }
    }

    public class SignumCultureSelectorFilter : IResourceFilter
    {
        public static Func<ResourceExecutingContext, CultureInfo> GetCurrentCultures;

        const string Culture_Key = "OldCulture";
        const string UICulture_Key = "OldUICulture";
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var culture = (CultureInfo)GetCurrentCultures?.Invoke(context);
            if (culture != null)
            {
                context.HttpContext.Items[Culture_Key] = CultureInfo.CurrentCulture;
                context.HttpContext.Items[UICulture_Key] = CultureInfo.CurrentUICulture;

                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
            CultureInfo.CurrentUICulture = context.HttpContext.Items[UICulture_Key] as CultureInfo ?? CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = context.HttpContext.Items[Culture_Key] as CultureInfo ?? CultureInfo.CurrentCulture;
        }
    }

    public class SignumTimesTrackerFilter : SignumDisposableResourceFilter
    {
        public SignumTimesTrackerFilter() : base("Signum_TimesTracker") {  }

        public override IDisposable GetResource(ResourceExecutingContext context)
        {
            string action = ProfilerActionSplitterAttribute.GetActionDescription(context);
            return TimeTracker.Start(action);
        }
    }

    public class SignumHeavyProfilerFilter : SignumDisposableResourceFilter
    {
        public SignumHeavyProfilerFilter() : base("Signum_HeavyProfiler") { }

        public override IDisposable GetResource(ResourceExecutingContext context)
        {
            return HeavyProfiler.Log("Web.API " + context.HttpContext.Request.Method, () => context.HttpContext.Request.GetDisplayUrl());
        }
    }

    public abstract class SignumDisposableResourceFilter : IResourceFilter
    {
        public string ResourceKey;

        public SignumDisposableResourceFilter(string key)
        {
            this.ResourceKey = key;
        }

        public abstract IDisposable GetResource(ResourceExecutingContext context);

        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            context.HttpContext.Items[ResourceKey] = GetResource(context);
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
            if (context.HttpContext.Items.TryGetValue(ResourceKey, out object result))
            {
                if (result != null)
                    ((IDisposable)result).Dispose();
            }
        }
    }
}