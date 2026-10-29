using System.Text.Json;
namespace DCWallet.Core;
public sealed record VaultProfile(string Id,string Name,string VaultPath);
public sealed class VaultProfiles(string dataDirectory)
{
 private string Root=>Path.GetFullPath(dataDirectory);
 private string ProfilesRoot=>Path.Combine(Root,"profiles");
 public IReadOnlyList<VaultProfile> List(){var result=new List<VaultProfile>();var legacy=Path.Combine(Root,"wallet.vault");if(File.Exists(legacy))result.Add(new("legacy","Основной",legacy));if(!Directory.Exists(ProfilesRoot))return result;
  foreach(var directory in Directory.EnumerateDirectories(ProfilesRoot)){
   var id=Path.GetFileName(directory);if(!Guid.TryParseExact(id,"N",out _)||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)continue;
   var vault=Path.Combine(directory,"wallet.vault");var metadata=Path.Combine(directory,"profile.json");if(!File.Exists(vault)||!File.Exists(metadata)||new FileInfo(metadata).Length>2048)continue;
   try{var name=JsonSerializer.Deserialize<string>(File.ReadAllText(metadata));if(!string.IsNullOrWhiteSpace(name)&&name.Length<=80)result.Add(new(id,name,vault));}catch(JsonException){}
  }return result;
 }
 public async Task<VaultProfile> CreateAsync(string name,VaultCredential credential,string password){if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>80)throw new ArgumentException("Название: от 1 до 80 символов.");var id=Guid.NewGuid().ToString("N");var directory=Path.Combine(ProfilesRoot,id);var path=Path.Combine(directory,"wallet.vault");
  if(credential.Kind=="Mnemonic")await Vault.CreateAsync(path,credential.Secret,password);else if(credential.Kind=="PrivateKey")await Vault.CreatePrivateKeyAsync(path,credential.Secret,password);else throw new ArgumentException("Неизвестный тип кошелька.");
  await File.WriteAllTextAsync(Path.Combine(directory,"profile.json"),JsonSerializer.Serialize(name.Trim()));return new(id,name.Trim(),path);
 }
 public VaultProfile Find(string id)=>List().SingleOrDefault(p=>p.Id==id)??throw new ArgumentException("Профиль не найден.");
}
