using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using SonnetDB.Data;
using AdoDbType = System.Data.DbType;

namespace SqlSugar.SonnetDB
{
    public sealed class SonnetDBProvider : AdoProvider
    {
        public override IDbConnection Connection
        {
            get
            {
                if (_DbConnection == null)
                {
                    _DbConnection = new SndbConnection(Context.CurrentConnectionConfig.ConnectionString);
                }

                return _DbConnection;
            }
            set => _DbConnection = value;
        }

        public override void BeginTran(string transactionName)
        {
            base.BeginTran();
        }

        public override void BeginTran(IsolationLevel iso, string transactionName)
        {
            base.BeginTran(iso);
        }

        public override IDataAdapter GetAdapter()
        {
            return new SonnetDBDataAdapter();
        }

        public override DbCommand GetCommand(string sql, SugarParameter[] parameters)
        {
            if (CommandType != System.Data.CommandType.Text)
            {
                throw new NotSupportedException(
                    "SonnetDB 的 SqlSugar 提供程序仅支持普通 SQL 命令，不支持存储过程或表直接命令。");
            }

            if (sql == Environment.NewLine)
            {
                sql = "SELECT 0";
            }

            var command = new SndbCommand(sql, (SndbConnection)Connection)
            {
                CommandType = System.Data.CommandType.Text,
                CommandTimeout = CommandTimeOut
            };

            if (Transaction != null)
            {
                command.Transaction = (SndbTransaction)Transaction;
            }

            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange((SndbParameter[])ToIDbDataParameter(parameters));
            }

            CheckConnection();
            return command;
        }

        public override void SetCommandToAdapter(IDataAdapter dataAdapter, DbCommand command)
        {
            ((SonnetDBDataAdapter)dataAdapter).SelectCommand = (SndbCommand)command;
        }

        public override IDataParameter[] ToIDbDataParameter(params SugarParameter[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                // SqlSugar 对无参数命令约定返回 null，保持既有调用行为。
                return null!;
            }

            var result = new SndbParameter[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                ThrowIfUnsupportedParameterType(parameter);
                var direction = parameter.Direction == 0 ? ParameterDirection.Input : parameter.Direction;
                if (direction != ParameterDirection.Input)
                {
                    throw new NotSupportedException(
                        "SonnetDB 不支持输出、输入输出或返回值参数。");
                }

                result[index] = new SndbParameter
                {
                    ParameterName = NormalizeParameterName(parameter.ParameterName),
                    DbType = NormalizeDbType(parameter.DbType),
                    Direction = ParameterDirection.Input,
                    Size = parameter.Size,
                    Value = NormalizeValue(parameter.Value, parameter.DbType)
                };
            }

            return result;
        }

        private static string NormalizeParameterName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("SonnetDB 参数必须具有名称。", nameof(name));
            }

            return name[0] is '@' or ':' ? name : "@" + name;
        }

        private static AdoDbType NormalizeDbType(AdoDbType dbType)
        {
            return dbType switch
            {
                AdoDbType.UInt16 => AdoDbType.Int32,
                AdoDbType.UInt32 => AdoDbType.Int64,
                AdoDbType.UInt64 => AdoDbType.Int64,
                AdoDbType.DateTimeOffset => AdoDbType.DateTime,
                AdoDbType.Guid => AdoDbType.String,
                _ => dbType
            };
        }

        private static object NormalizeValue(object value, AdoDbType dbType)
        {
            if (value == null || value == DBNull.Value)
            {
                return DBNull.Value;
            }

            if (value is DateOnly dateOnly)
            {
                return dateOnly.ToDateTime(TimeOnly.MinValue);
            }

            return dbType switch
            {
                AdoDbType.UInt16 => NormalizeUInt16(value),
                AdoDbType.UInt32 => NormalizeUInt32(value),
                AdoDbType.UInt64 => NormalizeUInt64(value),
                AdoDbType.Guid => value.ToString()!,
                AdoDbType.DateTimeOffset => NormalizeDateTimeOffset(value),
                _ => value
            };
        }

        private static int NormalizeUInt16(object value)
        {
            try
            {
                return checked(Convert.ToInt32(value, CultureInfo.InvariantCulture));
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                throw new NotSupportedException("SonnetDB 的 UInt16 参数值无法转换为关系表支持的整数类型。", exception);
            }
        }

        private static long NormalizeUInt32(object value)
        {
            try
            {
                return checked(Convert.ToInt64(value, CultureInfo.InvariantCulture));
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                throw new NotSupportedException("SonnetDB 的 UInt32 参数值无法转换为关系表支持的整数类型。", exception);
            }
        }

        private static long NormalizeUInt64(object value)
        {
            ulong unsignedValue;
            try
            {
                unsignedValue = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                throw new NotSupportedException("SonnetDB 的 UInt64 参数值无法转换为关系表支持的整数类型。", exception);
            }

            if (unsignedValue > long.MaxValue)
            {
                throw new NotSupportedException("SonnetDB 的 UInt64 参数值超过关系表整数类型的最大范围。请改用有符号整数或 STRING。");
            }

            return (long)unsignedValue;
        }

        private static DateTime NormalizeDateTimeOffset(object value)
        {
            if (value is DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset.UtcDateTime;
            }

            if (value is DateTime dateTime)
            {
                return dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
            }

            throw new NotSupportedException("SonnetDB 的 DateTimeOffset 参数必须是 DateTimeOffset 或 DateTime。");
        }

        private static void ThrowIfUnsupportedParameterType(SugarParameter parameter)
        {
            if (parameter.DbType is AdoDbType.Decimal or AdoDbType.Currency or AdoDbType.VarNumeric ||
                parameter.Value is decimal)
            {
                throw new NotSupportedException(
                    "SonnetDB 关系表不支持精确的 DECIMAL/NUMERIC 参数。请使用 double 或 float，或使用 STRING 并自行处理精度。");
            }

            if (parameter.DbType == AdoDbType.Time || parameter.Value is TimeSpan or TimeOnly)
            {
                throw new NotSupportedException(
                    "SonnetDB 关系表不支持 TimeOnly 或 TimeSpan 参数。请使用 DateTime 或 STRING 表示时间值。");
            }
        }
    }
}
