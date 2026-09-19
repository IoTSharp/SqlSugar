# SonnetDB 冒烟验证

这是 SqlSugar SonnetDB 提供程序的独立 `net10.0` 冒烟项目。

运行：

```powershell
dotnet run --project Src/Asp.NetCore2/SonnetDBTest/SonnetDBTest.csproj
```

项目在临时目录创建 SonnetDB 数据库，结束后自动清理。它覆盖：

- 代码优先建表、自增主键、视图、`JSON` 和 `BLOB` 元数据；
- SqlSugar 的插入、查询、更新、删除与 `LIMIT/OFFSET` 分页；
- `information_schema`、`CURRENT_UTC_DATETIME()`、`DATE_PART()`、`json_value()`；
- 关系表 `BeginTran`、读己之写、`ROLLBACK` 和 `COMMIT`；
- 已知方言边界：单结果集、无 CTE、无 `RIGHT JOIN`，以及不接受多条 SQL 拼接。

当前 SonnetDB NuGet 包仅提供 `net10.0` ADO.NET 驱动，因此该项目和提供程序都必须使用 .NET 10 或更高版本。

提供程序通过旁加载程序集进行动态类型创建；NativeAOT、激进裁剪和 single-file 捆绑发布暂未支持，请使用保留独立 DLL 的普通发布方式。
