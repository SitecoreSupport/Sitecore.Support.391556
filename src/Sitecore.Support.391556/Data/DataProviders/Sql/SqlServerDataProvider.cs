namespace Sitecore.Support.Data.DataProviders.Sql
{
  using System;
  using System.Reflection;
  using Configuration;
  using Diagnostics;
  using Sitecore.Data;
  using Sitecore.Data.DataProviders;
  using Sitecore.Data.DataProviders.Sql;
  using Sitecore.Data.Items;
  using Threading;


  public class SqlServerDataProvider : Sitecore.Data.SqlServer.SqlServerDataProvider
  {
    #region Added
    protected Action<ItemDefinition, ItemChanges> UpdateItemDefinition;

    protected Action<ID, ItemChanges> UpdateItemFields;

    protected Action<ID, ID> OnItemSaved;

    protected Action<ItemChanges, CallContext> RemoveOldBlobs;

    protected static readonly string BlobsCheckSettingName = "PreventFromOrphanedBlobs";

    protected static bool PreventFromOrphanedBlobs
    {
      get
      {
        return Settings.GetBoolSetting("PreventFromOrphanedBlobs", true);
      }
    }

    public SqlServerDataProvider(string connectionString) : base(connectionString)
    {
      this.UpdateItemDefinition = (Action<ItemDefinition, ItemChanges>)this.GetDelegateForMethodInfo<Action<ItemDefinition, ItemChanges>>(this.GetMethodByName("UpdateItemDefinition"));
      this.UpdateItemFields = (Action<ID, ItemChanges>)this.GetDelegateForMethodInfo<Action<ID, ItemChanges>>(this.GetMethodByName("UpdateItemFields"));
      this.OnItemSaved = (Action<ID, ID>)this.GetDelegateForMethodInfo<Action<ID, ID>>(this.GetMethodByName("OnItemSaved"));
      this.RemoveOldBlobs = (Action<ItemChanges, CallContext>)this.GetDelegateForMethodInfo<Action<ItemChanges, CallContext>>(this.GetMethodByName("RemoveOldBlobs"));
    }

    protected Delegate GetDelegateForMethodInfo<T>(MethodInfo methodInfo)
    {
      return Delegate.CreateDelegate(typeof(T), this, methodInfo);
    }

    protected MethodInfo GetMethodByName(string name)
    {
      MethodInfo method = typeof(SqlDataProvider).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      Assert.IsNotNull(method, Sitecore.StringExtensions.StringExtensions.FormatWith("Was unable to get '{0}' method.", new object[]
      {
        name
      }));
      return method;
    }

    protected virtual void RemoveOldBlobsAsync(ItemChanges changes, CallContext context)
    {
      if (SqlServerDataProvider.PreventFromOrphanedBlobs)
      {
        ManagedThreadPool.QueueUserWorkItem(delegate (object state)
        {
          this.RemoveOldBlobs(changes, context);
        });
      }
    }
    #endregion

    #region Modified
    public override bool SaveItem(ItemDefinition itemDefinition, ItemChanges changes, CallContext context)
    {
      if (changes.HasPropertiesChanged || changes.HasFieldsChanged)
      {
        Action action = delegate
        {
          using (DataProviderTransaction dataProviderTransaction = this.Api.CreateTransaction())
          {
            if (changes.HasPropertiesChanged)
            {
              this.UpdateItemDefinition(itemDefinition, changes);
            }
            if (changes.HasFieldsChanged)
            {
              this.UpdateItemFields(itemDefinition.ID, changes);
            }
            dataProviderTransaction.Complete();
          }
        };
        Factory.GetRetryer().ExecuteNoResult(action);
      }
      this.RemoveOldBlobsAsync(changes, context);
      this.OnItemSaved(itemDefinition.ID, itemDefinition.TemplateID);
      return true;
    }
    #endregion
  }
}