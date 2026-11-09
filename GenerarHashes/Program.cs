string clave = "Xlafo27112005@";

string hash = BCrypt.Net.BCrypt.HashPassword(clave, 12);

Console.WriteLine(hash);
Console.WriteLine(BCrypt.Net.BCrypt.Verify(clave, hash));