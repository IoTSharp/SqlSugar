namespace SqlSugar.SonnetDB
{
    public class SonnetDBDeleteable<T> : DeleteableProvider<T>, IDeleteable<T> where T : class, new()
    {
        IDeleteable<T> IDeleteable<T>.With(string lockString)
        {
            SonnetDBDmlSupport.ValidateWith(lockString);
            return this;
        }
    }
}
