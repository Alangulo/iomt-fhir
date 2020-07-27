﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using EnsureThat;
using Microsoft.Health.Common.Handler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Health.Fhir.Ingest.Template
{
    public abstract class CollectionTemplateFactory<TInTemplate, TOutTemplate>
        where TInTemplate : class
        where TOutTemplate : class
    {
        private static readonly IResponsibilityHandler<TemplateContainer, TInTemplate> NotFoundHandler = new TemplateNotFoundHandler<TInTemplate>();

        protected CollectionTemplateFactory(params ITemplateFactory<TemplateContainer, TInTemplate>[] factories)
        {
            EnsureArg.IsNotNull(factories, nameof(factories));
            EnsureArg.HasItems(factories, nameof(factories));

            IResponsibilityHandler<TemplateContainer, TInTemplate> handler = new WrappedHandlerTemplateFactory<TemplateContainer, TInTemplate>(factories[0]);
            for (int i = 1; i < factories.Length; i++)
            {
                handler = handler.Chain(new WrappedHandlerTemplateFactory<TemplateContainer, TInTemplate>(factories[i]));
            }

            // Attach NotFoundHandler at the end of the chain to throw exception if we reach end with no factory found.
            TemplateFactories = handler.Chain(NotFoundHandler);
        }

        protected IResponsibilityHandler<TemplateContainer, TInTemplate> TemplateFactories { get; }

        protected abstract string TargetTemplateTypeName { get; }

        protected TOutTemplate Create(string input, ICollection<Exception> errorContext)
        {
            EnsureArg.IsNotNull(errorContext, nameof(errorContext));

            var rootContainer = JsonConvert.DeserializeObject<TemplateContainer>(input);
            if (!rootContainer.MatchTemplateName(TargetTemplateTypeName))
            {
                throw new InvalidTemplateException($"Expected {nameof(rootContainer.TemplateType)} value {TargetTemplateTypeName}, actual {rootContainer.TemplateType}.");
            }

            if (rootContainer.Template?.Type != JTokenType.Array)
            {
                throw new InvalidTemplateException($"Expected an array for the template property value for template type {TargetTemplateTypeName}.");
            }

            return BuildCollectionTemplate((JArray)rootContainer.Template, errorContext);
        }

        protected abstract TOutTemplate BuildCollectionTemplate(JArray templateCollection, ICollection<Exception> errorContext);
    }
}
