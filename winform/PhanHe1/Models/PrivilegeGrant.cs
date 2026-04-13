namespace PhanHe1.Models
{
    public class PrivilegeGrant
    {
        public string Grantee { get; set; }
        public string Privilege { get; set; }
        public string ObjectName { get; set; }
        public string ObjectType { get; set; }
        public string ColumnName { get; set; }
        public bool Grantable { get; set; }
    }
}
