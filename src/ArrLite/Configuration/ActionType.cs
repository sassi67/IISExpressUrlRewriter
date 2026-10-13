namespace ArrLite.Configuration
{
    /// <summary>Values mirror the <c>action/@type</c> enum in arrLite_schema.xml.</summary>
    public enum ActionType
    {
        None = 0,
        Rewrite = 1,
        Redirect = 2,
        CustomResponse = 3,
        AbortRequest = 4,
    }
}
