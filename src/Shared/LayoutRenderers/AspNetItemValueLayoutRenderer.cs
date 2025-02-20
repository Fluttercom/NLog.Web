﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Web.Internal;

#if !ASP_NET_CORE
using System.Web;
#else
#endif

namespace NLog.Web.LayoutRenderers
{
    /// <summary>
    /// ASP.NET Item variable.
    /// </summary>
    /// <remarks>
    /// Use this layout renderer to insert the value of the specified variable stored
    /// in the ASP.NET HttpContext.Current.Items dictionary.
    /// </remarks>
    /// <example>
    /// <para>You can set the value of an ASP.NET Item variable by using the following code:</para>
    /// <code lang="C#">
    /// <![CDATA[
    /// HttpContext.Current.Items["myvariable"] = 123;
    /// HttpContext.Current.Items["stringvariable"] = "aaa BBB";
    /// HttpContext.Current.Items["anothervariable"] = DateTime.Now;
    /// ]]>
    /// </code>
    /// <para>Example usage of ${aspnet-item}:</para>
    /// <code lang="NLog Layout Renderer">
    /// ${aspnet-item:variable=myvariable} - produces "123"
    /// ${aspnet-item:variable=anothervariable} - produces "01/01/2006 00:00:00"
    /// ${aspnet-item:variable=anothervariable:culture=pl-PL} - produces "2006-01-01 00:00:00"
    /// ${aspnet-item:variable=myvariable:padding=5} - produces "  123"
    /// ${aspnet-item:variable=myvariable:padding=-5} - produces "123  "
    /// ${aspnet-item:variable=stringvariable:upperCase=true} - produces "AAA BBB"
    /// </code>
    /// </example>
    [LayoutRenderer("aspnet-item")]
    public class AspNetItemValueLayoutRenderer : AspNetLayoutRendererBase
    {
        /// <summary>
        /// Gets or sets the item variable name.
        /// </summary>
        /// <docgen category='Rendering Options' order='10' />
        [DefaultParameter]
        public string Variable { get; set; }

        /// <summary>
        /// Gets or sets whether items with a dot are evaluated as properties or not
        /// </summary>
        /// <docgen category='Rendering Options' order='10' />
        public bool EvaluateAsNestedProperties { get; set; }

        /// <summary>
        /// Format string for conversion from object to string.
        /// </summary>
        /// <docgen category='Rendering Options' order='10' />
        public string Format { get; set; }

        /// <summary>
        /// Gets or sets the culture used for rendering.
        /// </summary>
        /// <docgen category='Rendering Options' order='10' />
        public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

        /// <summary>
        /// Renders the specified ASP.NET Item value and appends it to the specified <see cref="StringBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="StringBuilder" /> to append the rendered data to.</param>
        /// <param name="logEvent">Logging event.</param>
        protected override void DoAppend(StringBuilder builder, LogEventInfo logEvent)
        {
            if (Variable == null)
            {
                return;
            }

            var context = HttpContextAccessor.HttpContext;
            var value = PropertyReader.GetValue(Variable, context?.Items, (items, key) => LookupItemValue(items, key), EvaluateAsNestedProperties);
            var formatProvider = GetFormatProvider(logEvent, Culture);
            builder.AppendFormattedValue(value, Format, formatProvider, ValueFormatter);
        }

#if !ASP_NET_CORE
        private static object LookupItemValue(System.Collections.IDictionary items, string key)
        {
            return items?.Count > 0 && items.Contains(key) ? items[key] : null;
        }
#else
        private static object LookupItemValue(IDictionary<object, object> items, string key)
        {
            return items != null && items.TryGetValue(key, out var itemValue) ? itemValue : null;
        }
#endif
    }
}