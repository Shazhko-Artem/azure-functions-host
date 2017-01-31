﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Tables
{
    internal class CollectorArgumentBindingProvider : IStorageTableArgumentBindingProvider
    {
        public IStorageTableArgumentBinding TryCreate(ParameterInfo parameter)
        {
            if (!parameter.ParameterType.IsGenericType ||
                (parameter.ParameterType.GetGenericTypeDefinition() != typeof(ICollector<>)))
            {
                return null;
            }

            Type entityType = GetCollectorItemType(parameter.ParameterType);
                        
            if (!TableClient.ImplementsOrEqualsITableEntity(entityType) && !TypeUtility.IsJObject(entityType))
            {
                TableClient.VerifyContainsProperty(entityType, "RowKey");
                TableClient.VerifyContainsProperty(entityType, "PartitionKey");
            }

            return CreateBinding(entityType);
        }

        private static Type GetCollectorItemType(Type queryableType)
        {
            Type[] genericArguments = queryableType.GetGenericArguments();
            var itemType = genericArguments[0];
            return itemType;
        }

        private static IStorageTableArgumentBinding CreateBinding(Type entityType)
        {
            if (TableClient.ImplementsOrEqualsITableEntity(entityType))
            {
                Type genericType = typeof(TableEntityCollectorArgumentBinding<>).MakeGenericType(entityType);
                return (IStorageTableArgumentBinding)Activator.CreateInstance(genericType);
            }
            else
            {
                Type genericType = typeof(PocoEntityCollectorArgumentBinding<>).MakeGenericType(entityType);
                return (IStorageTableArgumentBinding)Activator.CreateInstance(genericType);
            }
        }

        private class TableEntityCollectorArgumentBinding<TElement> : IStorageTableArgumentBinding
            where TElement : ITableEntity
        {
            public FileAccess Access
            {
                get { return FileAccess.Write; }
            }

            public Type ValueType
            {
                get { return typeof(ICollector<TElement>); }
            }

            public Task<IValueProvider> BindAsync(IStorageTable value, ValueBindingContext context)
            {
                TableEntityWriter<TElement> tableWriter = new TableEntityWriter<TElement>(value);
                IValueProvider provider = new TableEntityCollectorBinder<TElement>(value, tableWriter, typeof(ICollector<TElement>));
                return Task.FromResult(provider);
            }
        }

        private class PocoEntityCollectorArgumentBinding<TElement> : IStorageTableArgumentBinding
        {
            public FileAccess Access
            {
                get { return FileAccess.Write; }
            }

            public Type ValueType
            {
                get { return typeof(ICollector<TElement>); }
            }

            public Task<IValueProvider> BindAsync(IStorageTable value, ValueBindingContext context)
            {
                PocoEntityWriter<TElement> collector = new PocoEntityWriter<TElement>(value);
                IValueProvider provider = new PocoEntityCollectorBinder<TElement>(value, collector, typeof(ICollector<TElement>));
                return Task.FromResult(provider);
            }
        }
    }
}