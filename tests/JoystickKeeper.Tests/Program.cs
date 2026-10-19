using JoystickKeeper;
int fails = 0;
void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
var path = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "fixtures", "actionmaps.xml");
var fixedPath = Path.Combine(Path.GetTempPath(), "jk-fixed.xml");
var original = File.ReadAllText(path);
var profile = new Profile { Devices = {
  new() { Key="231D:0200", ProductName="R", VendorId=0x231D, ProductId=0x200, StarCitizenSlot=1 },
  new() { Key="231D:012D", ProductName="STECS", VendorId=0x231D, ProductId=0x12D, StarCitizenSlot=2 },
  new() { Key="231D:011F", ProductName="Rudder", VendorId=0x231D, ProductId=0x11F, StarCitizenSlot=3 },
  new() { Key="231D:0201", ProductName="L", VendorId=0x231D, ProductId=0x201, StarCitizenSlot=4 },
  new() { Key="1532:0266", ProductName="Huntsman", VendorId=0x1532, ProductId=0x266, StarCitizenSlot=5 },
}};
// Today's scan: STECS, Xbox (gamepad), R, L, Rudder, Huntsman
var scanned = new List<ScannedDevice> {
  new() { Key="231D:012D", GameSlot=1 }, new() { Key="046D:0000", GameSlot=0 },
  new() { Key="231D:0200", GameSlot=2 }, new() { Key="231D:0201", GameSlot=3 },
  new() { Key="231D:011F", GameSlot=4 }, new() { Key="1532:0266", GameSlot=5 } };
var (map, missing) = BindingsFixer.ComputeMap(profile, scanned);
Console.WriteLine(string.Join(" ", map.OrderBy(k=>k.Key).Select(k=>$"{k.Key}->{k.Value}")));
Check(map[1]==2 && map[2]==1 && map[3]==4 && map[4]==3 && map[5]==5 && missing.Count==0, "map matches today's order");
Check(map.Values.Distinct().Count()==map.Count, "map is a clean swap");
var fixedText = BindingsFixer.Remap(original, map);
File.WriteAllText(fixedPath, fixedText);
Check(fixedText.Contains("<rebind input=\"js2_x\"/>") && !fixedText.Contains("js1_x\""), "roll (right stick X) moved js1 -> js2");
Check(fixedText.Contains("js3_button5") && fixedText.Contains("js4_x\""), "left stick js4 -> js3, rudder js3 -> js4");
Check(fixedText.Contains("input=\"js2_ \""), "cleared bindings move too");
var opts = StarCitizen.ReadJoysticks(fixedPath);
Check(opts.First(o=>o.Instance==2).ProductId==0x200 && opts.First(o=>o.Instance==1).ProductId==0x12D && opts.First(o=>o.Instance==4).ProductId==0x11F, "options blocks renumbered with their devices");
var order = System.Text.RegularExpressions.Regex.Matches(fixedText, "type=\"joystick\" instance=\"(\\d+)\"").Select(m=>m.Groups[1].Value);
Check(string.Join("",order)=="12345678", "options blocks kept in order: " + string.Join("",order));
Check(System.Xml.Linq.XDocument.Parse(fixedText) != null, "still valid XML");
Check(fixedText.Contains("\r\n") && !fixedText.Replace("\r\n","").Contains('\n'), "Windows line endings kept");
var back = BindingsFixer.Remap(fixedText, BindingsFixer.Invert(map));
Check(back == original, "putting it back gives the exact original file");
// Rudder unplugged: others still placed, leftover slot filled
var (map2, missing2) = BindingsFixer.ComputeMap(profile, scanned.Where(s=>s.Key!="231D:011F").Select(s => new ScannedDevice{Key=s.Key, GameSlot = s.GameSlot>4?4:s.GameSlot}).ToList());
Check(missing2.SequenceEqual(new[]{"Rudder"}) && map2.Values.Distinct().Count()==map2.Count, "unplugged device reported, map still a clean swap");
Check(BindingsFixer.IsIdentity(BindingsFixer.ComputeMap(profile, new List<ScannedDevice>{ new(){Key="231D:0200",GameSlot=1}, new(){Key="231D:012D",GameSlot=2}, new(){Key="231D:011F",GameSlot=3}, new(){Key="231D:0201",GameSlot=4}, new(){Key="1532:0266",GameSlot=5}}).map), "right order means no change");
Console.WriteLine(fails==0 ? "ALL PASSED" : $"{fails} FAILED"); return fails;
