﻿using System;
using System.Linq;
using System.Web.Http;
using Microsoft.AspNet.OData.Batch;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Builder;
using DevExpress.Xpo.Metadata;
using ODataService.Helpers;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;

namespace WebApplication1
{
    public static class WebApiConfig {
        public static void Register(HttpConfiguration config) {
            config.Count().Filter().OrderBy().Expand().Select().MaxTop(null);
            ODataModelBuilder modelBuilder = CreateODataModelBuilder();

            ODataBatchHandler batchHandler =
                new DefaultODataBatchHandler(GlobalConfiguration.DefaultServer);

            config.MapODataServiceRoute(
                routeName: "ODataRoute",
                routePrefix: null,
                model: modelBuilder.GetEdmModel(),
                batchHandler: batchHandler);
        }

        static ODataModelBuilder CreateODataModelBuilder() {
            ODataModelBuilder builder = new ODataConventionModelBuilder();

            // Approach 1: Automatically add all persistent classes to EDM
            // This approach has a naming convention: an OData controller 
            // name must match the corresponding XPO class name

            var dictionary = new ReflectionDictionary();
            foreach(var type in ConnectionHelper.GetPersistentTypes()) {
                XPClassInfo classInfo = dictionary.GetClassInfo(type);
                CreateEntitySet(classInfo, builder);
            }

            // Approach 2: Manually add persistent classes to EDM

            /*var documents = builder.EntitySet<BaseDocument>("BaseDocument");
            var customers = builder.EntitySet<Customer>("Customer");
            var orders = builder.EntitySet<Order>("Order");
            var contracts = builder.EntitySet<Contract>("Contract");
            var products = builder.EntitySet<Product>("Product");
            var orderDetails = builder.EntitySet<OrderDetail>("OrderDetail");
            documents.EntityType.HasKey(t => t.ID);
            customers.EntityType.HasKey(t => t.CustomerID);
            products.EntityType.HasKey(t => t.ProductID);
            orderDetails.EntityType.HasKey(t => t.OrderDetailID);
            orders.EntityType.DerivesFrom<BaseDocument>();
            contracts.EntityType.DerivesFrom<BaseDocument>();*/

            // Add actions and functions to EDM

            builder.Action("InitializeDatabase");
            builder.Function("TotalSalesByYear")
                .Returns<decimal>()
                .Parameter<int>("year");

            return builder;
        }

        static EntitySetConfiguration CreateEntitySet(XPClassInfo classInfo, ODataModelBuilder builder) {
            EntitySetConfiguration entitySetConfig = builder.EntitySets.FirstOrDefault(t => t.EntityType.ClrType == classInfo.ClassType);
            if(entitySetConfig != null) {
                return entitySetConfig;
            }
            EntityTypeConfiguration entityTypeConfig = builder.AddEntityType(classInfo.ClassType);
            entitySetConfig = builder.AddEntitySet(classInfo.ClassType.Name, entityTypeConfig);
            if(classInfo.PersistentBaseClass != null) {
                EntitySetConfiguration baseClassEntitySetConfig = CreateEntitySet(classInfo.PersistentBaseClass, builder);
                entityTypeConfig.DerivesFrom(baseClassEntitySetConfig.EntityType);
            } else {
                if(classInfo.KeyProperty is ReflectionFieldInfo) {
                    foreach(XPMemberInfo mi in classInfo.Members) {
                        if(mi.IsAliased) {
                            string aliasedExpr = ((PersistentAliasAttribute)mi.GetAttributeInfo(typeof(PersistentAliasAttribute))).AliasExpression;
                            var aliasedCriteria = CriteriaOperator.Parse(aliasedExpr) as OperandProperty;
                            if(!ReferenceEquals(null, aliasedCriteria) && aliasedCriteria.PropertyName == classInfo.KeyProperty.Name) {
                                entityTypeConfig.HasKey(classInfo.ClassType.GetProperty(mi.Name));
                                break;
                            }
                        }
                    }
                } else {
                    entityTypeConfig.HasKey(classInfo.ClassType.GetProperty(classInfo.KeyProperty.Name));
                }
            }
            return entitySetConfig;
        }
    }
}
