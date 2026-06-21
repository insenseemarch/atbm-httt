using Oracle.ManagedDataAccess.Client;

var services = new[] { "XEPDB1", "xepdb1", "PDBQLBV", "ORCL", "FREE" };
foreach (var svc in services)
{
    var cs = $"User Id=qlbv;Password=123;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME={svc})))";
    try
    {
        using var conn = new OracleConnection(cs);
        conn.Open();
        Console.WriteLine($"=== CONNECTED: qlbv@{svc} ===");
        RunChecks(conn);
        return;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL qlbv@{svc}: {ex.Message}");
    }
}

static void RunChecks(OracleConnection conn)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT table_name FROM user_tables WHERE table_name IN ('BACKUP_HISTORY','RESTORE_HISTORY') ORDER BY 1";
    using (var r = cmd.ExecuteReader())
    {
        Console.WriteLine("Tables:");
        while (r.Read()) Console.WriteLine("  " + r.GetString(0));
    }
    cmd.CommandText = "SELECT column_name FROM user_tab_columns WHERE table_name='BACKUP_HISTORY' ORDER BY column_id";
    var cols = new List<string>();
    using (var r = cmd.ExecuteReader()) while (r.Read()) cols.Add(r.GetString(0));
    Console.WriteLine("BACKUP_HISTORY columns: " + string.Join(", ", cols));
    cmd.CommandText = "SELECT COUNT(*) FROM backup_history";
    Console.WriteLine("backup_history COUNT: " + cmd.ExecuteScalar());
    cmd.CommandText = "SELECT COUNT(*) FROM restore_history";
    Console.WriteLine("restore_history COUNT: " + cmd.ExecuteScalar());
    cmd.CommandText = @"SELECT backup_id, TO_CHAR(backup_time,'DD/MM/YYYY HH24:MI:SS'), backup_type, NVL(file_name,'(null)'), NVL(status,'(null)'), SUBSTR(NVL(description,' '),1,60)
                        FROM backup_history ORDER BY backup_time DESC FETCH FIRST 10 ROWS ONLY";
    using (var r = cmd.ExecuteReader())
    {
        Console.WriteLine("Recent BACKUP_HISTORY:");
        if (!r.HasRows) Console.WriteLine("  (no rows)");
        while (r.Read())
            Console.WriteLine($"  id={r[0]} time={r[1]} type={r[2]} file={r[3]} status={r[4]} desc={r[5]}");
    }
    cmd.CommandText = @"SELECT restore_id, TO_CHAR(restore_time,'DD/MM/YYYY HH24:MI:SS'), restore_type, NVL(file_src,'(null)'), NVL(executed_by,'(null)'), NVL(status,'(null)')
                        FROM restore_history ORDER BY restore_time DESC FETCH FIRST 10 ROWS ONLY";
    using (var r = cmd.ExecuteReader())
    {
        Console.WriteLine("Recent RESTORE_HISTORY:");
        if (!r.HasRows) Console.WriteLine("  (no rows)");
        while (r.Read())
            Console.WriteLine($"  id={r[0]} time={r[1]} type={r[2]} src={r[3]} by={r[4]} status={r[5]}");
    }
}
