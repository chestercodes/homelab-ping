cd /work/src/migrations

dotnet tool restore

function GetValueOrFail($name){
    $v = [System.Environment]::GetEnvironmentVariable($name)
    if($null -eq $v){
        write-error "Cannot find $name"
        exit 1
    }

    return $v
}

$envname = GetValueOrFail "ENVNAME"
$thehost = "$envname-homelabmaindb-cluster-postgresql.$envname.svc"
$user = GetValueOrFail "MAINDB_USER"
$password = GetValueOrFail "MAINDB_PASSWORD"
$database = "pingapp"

$connectionString = "Host=$thehost;Port=5432;User ID=$user;Password=$password;Database=$database"

dotnet grate --connectionstring=$connectionString --databasetype postgresql
exit $LASTEXITCODE