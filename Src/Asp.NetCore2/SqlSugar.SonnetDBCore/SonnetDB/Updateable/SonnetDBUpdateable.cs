using System;
using System.Linq;
using System.Threading.Tasks;

namespace SqlSugar.SonnetDB
{
    public class SonnetDBUpdateable<T> : UpdateableProvider<T>, IUpdateable<T> where T : class, new()
    {
        public override int ExecuteCommand()
        {
            if (UpdateObjs == null || UpdateObjs.Length == 0)
            {
                return 0;
            }

            if (UpdateObjs.Length == 1)
            {
                return ExecuteSingleCommand();
            }

            return ExecuteEachCommand();
        }

        public override async Task<int> ExecuteCommandAsync()
        {
            if (UpdateObjs == null || UpdateObjs.Length == 0)
            {
                return 0;
            }

            if (UpdateObjs.Length == 1)
            {
                return await ExecuteSingleCommandAsync();
            }

            return await ExecuteEachCommandAsync();
        }

        private int ExecuteSingleCommand()
        {
            var isListUpdate = UpdateBuilder.IsListUpdate;
            try
            {
                // SonnetDB 不支持批量 UPDATE，单条记录仍可按普通 UPDATE 执行。
                UpdateBuilder.IsListUpdate = null;
                return base.ExecuteCommand();
            }
            finally
            {
                UpdateBuilder.IsListUpdate = isListUpdate;
            }
        }

        private async Task<int> ExecuteSingleCommandAsync()
        {
            var isListUpdate = UpdateBuilder.IsListUpdate;
            try
            {
                UpdateBuilder.IsListUpdate = null;
                return await base.ExecuteCommandAsync();
            }
            finally
            {
                UpdateBuilder.IsListUpdate = isListUpdate;
            }
        }

        private int ExecuteEachCommand()
        {
            var isNoTran = Context.Ado.IsNoTran();
            var result = 0;
            try
            {
                if (isNoTran)
                {
                    Context.Ado.BeginTran();
                }

                for (var index = 0; index < UpdateObjs.Length; index++)
                {
                    var updateable = (UpdateableProvider<T>)Clone();
                    updateable.UpdateObjs = new[] { UpdateObjs[index] };
                    updateable.UpdateBuilder.IsListUpdate = null;
                    updateable.UpdateBuilder.DbColumnInfoList = updateable.UpdateBuilder.DbColumnInfoList
                        .Where(it => it.TableId == index)
                        .ToList();
                    result += updateable.ExecuteCommand();
                }

                if (isNoTran)
                {
                    Context.Ado.CommitTran();
                }
            }
            catch
            {
                if (isNoTran)
                {
                    Context.Ado.RollbackTran();
                }
                throw;
            }

            return result;
        }

        private async Task<int> ExecuteEachCommandAsync()
        {
            var isNoTran = Context.Ado.IsNoTran();
            var result = 0;
            try
            {
                if (isNoTran)
                {
                    await Context.Ado.BeginTranAsync();
                }

                for (var index = 0; index < UpdateObjs.Length; index++)
                {
                    var updateable = (UpdateableProvider<T>)Clone();
                    updateable.UpdateObjs = new[] { UpdateObjs[index] };
                    updateable.UpdateBuilder.IsListUpdate = null;
                    updateable.UpdateBuilder.DbColumnInfoList = updateable.UpdateBuilder.DbColumnInfoList
                        .Where(it => it.TableId == index)
                        .ToList();
                    result += await updateable.ExecuteCommandAsync();
                }

                if (isNoTran)
                {
                    await Context.Ado.CommitTranAsync();
                }
            }
            catch
            {
                if (isNoTran)
                {
                    await Context.Ado.RollbackTranAsync();
                }
                throw;
            }

            return result;
        }

        IUpdateable<T> IUpdateable<T>.With(string lockString)
        {
            SonnetDBDmlSupport.ValidateWith(lockString);
            return this;
        }

    }
}
