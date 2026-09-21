using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using System.Xml;

namespace WardogsFastCalc {
 public sealed class SavedTarget {
  public string Origin { get; set; }
  public string Target { get; set; }
  public string Distance { get; set; }
  public string Summary { get; set; }
  public string Name { get; set; }
  public bool IsFavorite { get; set; }
  public string DisplayName { get { return (IsFavorite?"★ ":"")+(String.IsNullOrEmpty(Name)?"Target "+Target:Name); } }
  public string Detail { get { return "Target "+Target+"  ·  "+Summary; } }
  public string FullDetail { get { return DisplayName+"\nMortar: "+Origin+"\nTarget: "+Target+"\nDistance override: "+(String.IsNullOrWhiteSpace(Distance)?"none":Distance+" m")+"\n"+Summary; } }
  public override string ToString() { return DisplayName; }
 }
 public sealed partial class Controller {
  public Window Window;
  public TextBox Origin, Target, Distance, TargetName, HistorySearch;
  public ListBox History;
  public Solution Current;
  public MortarScene Scene;
  public readonly List<SavedTarget> Saved = new List<SavedTarget>();
  bool loading;
  readonly string statePath;
  readonly Func<ModifierKeys> readModifiers;
  public Controller(string path,Func<ModifierKeys> modifiers=null) {
   readModifiers=modifiers??(()=>Keyboard.Modifiers);
   statePath = path;
   using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) Window = (Window)XamlReader.Load(stream);
   using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.ico")) {
    Window.Icon = BitmapFrame.Create(icon,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
   }
   Origin = Find<TextBox>("Origin"); Target = Find<TextBox>("Target"); Distance = Find<TextBox>("Distance"); History = Find<ListBox>("History");
   TargetName=Find<TextBox>("TargetName");HistorySearch=Find<TextBox>("HistorySearch");
   Scene=new MortarScene(Find<Viewport3D>("SceneViewport"),Find<Canvas>("SceneLabels"),Find<Border>("SceneHost"),readModifiers);
   Find<Button>("ViewTop").Click+=delegate{Scene.ToggleTop();};
   Find<Button>("ViewReset").Click+=delegate{Scene.ResetCamera();};
   Find<ScrollViewer>("BodyScroll").SizeChanged+=delegate{UpdateLayout();};
   Origin.TextChanged += Changed; Target.TextChanged += Changed; Distance.TextChanged += Changed;
   foreach (var box in new[] {Origin, Target, Distance, TargetName}) box.GotKeyboardFocus += delegate(object s, KeyboardFocusChangedEventArgs e) { ((TextBox)s).SelectAll(); };
   Find<Button>("Save").Click += delegate { SaveTarget(); };
   Find<Button>("Copy").Click += delegate { CopyCallout(); };
   Find<Button>("NewTarget").Click += delegate { NewTarget(); };
   Find<Button>("Help").Click += delegate { ShowHelp(); };
   Find<Button>("Example").Click += delegate { LoadExample(); };
   Find<CheckBox>("Pin").Checked += delegate { Window.Topmost = true; };
   Find<CheckBox>("Pin").Unchecked += delegate { Window.Topmost = false; };
   History.MouseDoubleClick += delegate(object s,MouseButtonEventArgs e) { if(ItemsControl.ContainerFromElement(History,e.OriginalSource as DependencyObject) is ListBoxItem)UseSelectedTarget(); };
   History.SelectionChanged+=delegate{UpdateHistoryActions();};
   HistorySearch.TextChanged+=delegate{RefreshHistory();};
   Find<Button>("UseTarget").Click+=delegate{UseSelectedTarget();};
   Find<Button>("RestoreSetup").Click+=delegate{Recall();};
   Find<Button>("Favorite").Click+=delegate{ToggleFavorite();};
   Find<Button>("RenameTarget").Click+=delegate{ShowRename();};
   Find<Button>("DeleteTarget").Click+=delegate{RemoveSelected();};
   Window.PreviewKeyDown += OnKey;
   Window.Closing += delegate { Persist(); };
   Window.Loaded += delegate { UpdateLayout(); Focus(Origin); };
   ConfigureDesktop();
   LoadState(); Update(); RefreshHistory();
  }
  public T Find<T>(string name) where T : class { return Window.FindName(name) as T; }
  static Brush Color(string value) { return (Brush)new BrushConverter().ConvertFromString(value); }
  void Set(string name, string text) { Find<TextBlock>(name).Text = text; }
  void Changed(object sender, TextChangedEventArgs e) { if (!loading) Update(); }
  public void Focus(TextBox box) { box.Focus(); box.SelectAll(); }
  public void SetInputs(string a, string b, string distance) {
   loading=true; Origin.Text=a; Target.Text=b; Distance.Text=distance; loading=false; Update();
  }
  public void LoadExample() { TargetName.Text="";SetInputs("x98.43, y110.38", "x94.53, y109.03", ""); Focus(Target); }
  public void NewTarget() { TargetName.Text="";SetInputs(Origin.Text,"",""); Focus(Target); }
  public void Update() {
   Current=null;
   Coordinate a,b; double number; double? distance=null;
   string error=null;
   if (!Calculator.TryCoordinate(Origin.Text, out a)) error=String.IsNullOrWhiteSpace(Origin.Text) ? "Enter your mortar coordinates. Example: x98.43, y110.38" : "Your mortar: enter one X/Y pair, each from 0 to 164.";
   if (!Calculator.TryCoordinate(Target.Text, out b) && error==null) error=String.IsNullOrWhiteSpace(Target.Text) ? "Enter target coordinates to get a bearing and range." : "Target: enter one X/Y pair, each from 0 to 164.";
   if (error==null && !String.IsNullOrWhiteSpace(Distance.Text)) {
    if (!Calculator.TryNumber(Distance.Text.Trim(),out number) || number<=0 || number>25000) error="Distance: use a number above 0, up to 25000 meters.";
    else distance=number;
   }
   if (error==null) { try { Current=Calculator.Solve(a,b,distance); } catch (ArgumentException ex) { error=ex.Message; } }
   Find<Button>("Save").IsEnabled=Current!=null;
   Find<Button>("Copy").IsEnabled=Current!=null;
   if (Current==null) {
    Set("InputStatus",error); Set("RangeStatus","WAITING FOR VALID COORDINATES");
    Find<TextBlock>("RangeStatus").Foreground=Color("#A7B5BE");
    Set("Bearing","—"); Set("Range","—"); Set("Mil","— MIL");
    Set("Direction","North 0° · East 90°"); Set("RangeDetail","meters");
    Set("Instruction","Paste your mortar and target coordinates. Then match the compass bearing and the left-side RNG readout in the game.");
    Scene.Update(null);Set("SceneState","Enter coordinates to align");return;
   }
   var s=Current;
   Set("InputStatus", s.Overridden ? String.Format(CultureInfo.InvariantCulture,"OVERRIDE ACTIVE · grid distance {0:0.0} m. Clear distance to return to automatic range.",s.Distance) : "Using coordinate distance · updates instantly.");
   Set("Bearing",(Math.Round(s.Bearing,1)%360).ToString("000.0",CultureInfo.InvariantCulture)+"°");
   Set("Direction",s.Direction+" · compass heading");
   Set("Range",s.Range.ToString("0",CultureInfo.InvariantCulture));
   Set("RangeDetail",s.Overridden ? "meters · manual override" : "meters · calculated");
   Set("Mil",s.Mil.HasValue ? "~"+s.Mil.Value.ToString("0",CultureInfo.InvariantCulture)+" MIL" : "— MIL");
   Set("RangeStatus",s.InRange ? "WITHIN L81 RANGE  /  132–684 M" : s.Range<132 ? "TOO CLOSE  /  L81 MINIMUM 132 M" : "TOO FAR  /  L81 MAXIMUM 684 M");
   Find<TextBlock>("RangeStatus").Foreground=Color(s.InRange ? "#91D6B6" : "#FFBC79");
   Set("Instruction",s.InRange ? String.Format(CultureInfo.InvariantCulture,"Turn to {0:000.0}° {1}. Adjust elevation until the left-side RNG reads {2:0} m. Confirm against your map ping; use the shell camera to check impact.",Math.Round(s.Bearing,1)%360,s.Direction,s.Range)
    : "This distance is outside the community L81 table. Move your in-game mortar or select another target; no elevation estimate is available.");
   Scene.Update(s);Set("SceneState",s.InRange?"Target direction · live from coordinates":"Outside L81 range · elevation unavailable");
  }
  static string CleanName(string value) {
   string name=new string((value??"").Where(c=>!Char.IsControl(c)).ToArray()).Trim();
   if(name.Length>60)name=name.Substring(0,60);
   // Avoid an incomplete surrogate after truncating a name containing emoji.
   if(name.Length>0&&Char.IsHighSurrogate(name[name.Length-1]))name=name.Substring(0,name.Length-1);
   try{XmlConvert.VerifyXmlChars(name);}catch(XmlException){return "";}return name;
  }
  static bool SameCoordinate(string first,string second){Coordinate a,b;return Calculator.TryCoordinate(first,out a)&&Calculator.TryCoordinate(second,out b)&&a.X==b.X&&a.Y==b.Y;}
  static bool SameDistance(string first,string second){double a,b;return String.IsNullOrWhiteSpace(first)&&String.IsNullOrWhiteSpace(second)||Calculator.TryNumber(first.Trim(),out a)&&Calculator.TryNumber(second.Trim(),out b)&&a==b;}
  public void SaveTarget() {
   if(Current==null)return;
   var existing=Saved.FirstOrDefault(t=>SameCoordinate(t.Origin,Origin.Text)&&SameCoordinate(t.Target,Target.Text)&&SameDistance(t.Distance,Distance.Text));
   if(existing==null&&Saved.Count>=20&&Saved.All(t=>t.IsFavorite)){Set("HistoryHint","All 20 saved targets are favorites. Remove or unstar one before saving another.");return;}
   if(existing!=null)Saved.Remove(existing);
   var saved=new SavedTarget{Origin=Origin.Text.Trim(),Target=Target.Text.Trim(),Distance=Distance.Text.Trim(),Name=CleanName(TargetName.Text),IsFavorite=existing!=null&&existing.IsFavorite,
    Summary=String.Format(CultureInfo.InvariantCulture,"Saved setup: {0:000.0}° {1} · {2:0} m{3}",Math.Round(Current.Bearing,1)%360,Current.Direction,Current.Range,Current.Overridden?" · override":"")};
   if(existing!=null&&saved.Name.Length==0)saved.Name=existing.Name;
   Saved.Insert(0,saved);
   if(Saved.Count>20)Saved.Remove(Saved.Last(t=>!t.IsFavorite));
   HistorySearch.Text="";RefreshHistory(saved);
   Set("HistoryHint",Persist()?"Saved. Enter uses this target; Shift+Enter restores its complete setup.":"Saved for this session only. Disk save failed; see the status below.");
  }
  void RefreshHistory(SavedTarget select=null){
   select=select??History.SelectedItem as SavedTarget;string query=HistorySearch.Text.Trim();
   var visible=Saved.Where(t=>query.Length==0||(t.Name??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||t.Target.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||t.Origin.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).OrderByDescending(t=>t.IsFavorite).ToList();
   History.ItemsSource=visible;History.SelectedItem=visible.Contains(select)?select:visible.FirstOrDefault();
   Set("HistoryCount",visible.Count+" / "+Saved.Count);
   Set("HistoryHint",Saved.Count==0?"No saved targets. Add a name above, then press Enter.":visible.Count==0?"No matches. Esc in search clears the filter.":"Ctrl+H selects · Enter uses target · Shift+Enter restores · F2 renames");
   UpdateHistoryActions();
  }
  void UpdateHistoryActions(){
   var selected=History.SelectedItem as SavedTarget;
   foreach(string name in new[]{"UseTarget","RestoreSetup","Favorite","RenameTarget","DeleteTarget"})Find<Button>(name).IsEnabled=selected!=null;
   Find<Button>("Favorite").Content=selected!=null&&selected.IsFavorite?"★ Unstar":"☆ Favorite";
  }
  public void Recall(){var t=History.SelectedItem as SavedTarget;if(t==null)return;SetInputs(t.Origin,t.Target,t.Distance);TargetName.Text=t.Name??"";Focus(Target);Set("HistoryHint","Restored saved mortar position, target, and distance override.");}
  public void UseSelectedTarget(){var t=History.SelectedItem as SavedTarget;if(t==null)return;SetInputs(Origin.Text,t.Target,"");TargetName.Text=t.Name??"";Focus(Target);Set("HistoryHint","Kept your current mortar position. Cleared the old override and recalculated distance.");}
  public void ToggleFavorite(){var t=History.SelectedItem as SavedTarget;if(t==null)return;bool focused=History.IsKeyboardFocusWithin;t.IsFavorite=!t.IsFavorite;RefreshHistory(t);if(focused)FocusHistory();Persist();}
  public void RenameSelected(string name){var t=History.SelectedItem as SavedTarget;if(t==null)return;string old=t.Name;t.Name=CleanName(name);if(TargetName.Text==(old??"")&&SameCoordinate(Origin.Text,t.Origin)&&SameCoordinate(Target.Text,t.Target)&&SameDistance(Distance.Text,t.Distance))TargetName.Text=t.Name;RefreshHistory(t);Persist();}
  void ShowRename(){
   var selected=History.SelectedItem as SavedTarget;if(selected==null)return;
   var dialog=new Window{Title="Rename saved target",Owner=Window,Icon=Window.Icon,Width=420,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Color("#101419"),Foreground=Color("#EDF0F3"),Resources=Window.Resources};
   var panel=new StackPanel{Margin=new Thickness(20)};panel.Children.Add(new TextBlock{Text="Target name",Margin=new Thickness(0,0,0,10)});
   var input=new TextBox{Text=selected.Name??"",MaxLength=60,FontFamily=new FontFamily("Segoe UI"),FontSize=15};panel.Children.Add(input);
   var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};
   var cancel=new Button{Content="Cancel",IsCancel=true,Margin=new Thickness(0,0,8,0)};var save=new Button{Content="Rename",IsDefault=true};save.Click+=delegate{dialog.DialogResult=true;};buttons.Children.Add(cancel);buttons.Children.Add(save);panel.Children.Add(buttons);dialog.Content=panel;dialog.Loaded+=delegate{input.Focus();input.SelectAll();};
   if(dialog.ShowDialog()==true)RenameSelected(input.Text);FocusHistory();
  }
  void FocusHistory(){
   History.Focus();if(History.SelectedIndex<0&&History.Items.Count>0)History.SelectedIndex=0;
   if(History.SelectedItem!=null){History.ScrollIntoView(History.SelectedItem);History.UpdateLayout();var row=History.ItemContainerGenerator.ContainerFromItem(History.SelectedItem) as ListBoxItem;if(row!=null)row.Focus();}
  }
  public void RemoveSelected(){
   var t=History.SelectedItem as SavedTarget;if(t==null)return;
   int index=History.SelectedIndex;bool hadFocus=History.IsKeyboardFocusWithin;
   Saved.Remove(t);RefreshHistory();History.SelectedIndex=Math.Min(index,History.Items.Count-1);
   if(hadFocus)FocusHistory();Persist();
  }
  void CopyCallout(){if(Current==null)return;try{Clipboard.SetText(Current.Callout);Set("Footer","Copied callout to clipboard.");}catch(System.Runtime.InteropServices.ExternalException){Set("Footer","Clipboard is busy. Try Copy again.");}}
  public bool HandleShortcut(Key key, ModifierKeys modifiers) {
   if(modifiers==(ModifierKeys.Control|ModifierKeys.Shift)&&key==Key.C){CopyCallout();return true;}
   if(modifiers==ModifierKeys.Control){
    if(key==Key.D1||key==Key.NumPad1){Focus(Origin);return true;}
    if(key==Key.D2||key==Key.NumPad2){Focus(Target);return true;}
    if(key==Key.D3||key==Key.NumPad3){Focus(Distance);return true;}
    if(key==Key.D4||key==Key.NumPad4){Find<Border>("SceneHost").Focus();return true;}
    if(key==Key.D5||key==Key.NumPad5){Focus(TargetName);return true;}
    if(key==Key.F){Focus(HistorySearch);return true;}
    if(key==Key.N){NewTarget();return true;}
    if(key==Key.H){FocusHistory();return true;}
    if(key==Key.T){Find<CheckBox>("Pin").IsChecked=!Window.Topmost;return true;}
   }
   if(modifiers==ModifierKeys.None&&key==Key.F1){ShowHelp();return true;}
   if(modifiers==ModifierKeys.None&&HistorySearch.IsKeyboardFocused){
    if(key==Key.Escape){HistorySearch.Clear();return true;}
    if(key==Key.Down||key==Key.Enter){FocusHistory();return true;}
   }
   if(modifiers==ModifierKeys.Shift&&key==Key.Enter&&History.IsKeyboardFocusWithin){Recall();return true;}
   if(modifiers==ModifierKeys.None&&key==Key.Enter){if(History.IsKeyboardFocusWithin)UseSelectedTarget();else if(Keyboard.FocusedElement==Origin||Keyboard.FocusedElement==Target||Keyboard.FocusedElement==Distance||Keyboard.FocusedElement==TargetName)SaveTarget();else return false;return true;}
   if(modifiers==ModifierKeys.None&&key==Key.F2&&History.IsKeyboardFocusWithin){ShowRename();return true;}
   if(modifiers==ModifierKeys.None&&key==Key.F&&History.IsKeyboardFocusWithin){ToggleFavorite();return true;}
   if(modifiers==ModifierKeys.None&&key==Key.Delete&&History.IsKeyboardFocusWithin){RemoveSelected();return true;}
   return false;
  }
  void OnKey(object sender,KeyEventArgs e){if(HandleShortcut(e.Key,readModifiers()))e.Handled=true;}
  public bool Persist(){
   if(statePath==null)return true;
   string temp=statePath+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{
    var root=new XElement("WardogsFastCalc",new XElement("Origin",Origin.Text),new XElement("Target",Target.Text),new XElement("Distance",Distance.Text),new XElement("TargetName",CleanName(TargetName.Text)),new XElement("Pinned",Window.Topmost));
    WritePlacement(root);
    var history=new XElement("History");foreach(var t in Saved)history.Add(new XElement("Setup",new XAttribute("origin",t.Origin),new XAttribute("target",t.Target),new XAttribute("distance",t.Distance),new XAttribute("summary",t.Summary??""),new XAttribute("name",t.Name??""),new XAttribute("favorite",t.IsFavorite)));root.Add(history);
    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(statePath));
    new XDocument(root).Save(temp);
    if(File.Exists(statePath))File.Replace(temp,statePath,null);else File.Move(temp,statePath);
    if(Find<TextBlock>("Footer").Text.StartsWith("Could not save this session.",StringComparison.Ordinal))Set("Footer","Session saved.");
    return true;
   }catch(Exception ex){if(!(ex is IOException)&&!(ex is UnauthorizedAccessException)&&!(ex is ArgumentException))throw;Set("Footer","Could not save this session. Check folder permissions or free disk space.");return false;}
   finally{try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
  }
  void LoadState(){
   if(statePath==null||!File.Exists(statePath))return;
   try{
    XElement root;
    var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=131072,MaxCharactersFromEntities=1024 };
    using(var stream=new FileStream(statePath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
     if(stream.Length>131072)throw new InvalidDataException("Session exceeds size limit.");
     using(var reader=XmlReader.Create(stream,settings)) root=XDocument.Load(reader).Root;
    }
    if(root==null||root.Name!="WardogsFastCalc")throw new InvalidDataException("Unexpected session document.");
    string origin=(string)root.Element("Origin")??"",target=(string)root.Element("Target")??"",distance=(string)root.Element("Distance")??"";
    if(origin.Length>512||target.Length>512||distance.Length>12)throw new InvalidDataException("Session fields exceed input limits.");
    SetInputs(origin,target,distance);
    TargetName.Text=CleanName((string)root.Element("TargetName"));ReadPlacement(root);
    Find<CheckBox>("Pin").IsChecked=(string)root.Element("Pinned")=="true";
    var history=root.Element("History");if(history!=null)foreach(var t in history.Elements("Setup").Take(20)) {
     var saved=new SavedTarget{Origin=(string)t.Attribute("origin")??"",Target=(string)t.Attribute("target")??"",Distance=(string)t.Attribute("distance")??"",Summary=(string)t.Attribute("summary")??"Saved target",Name=CleanName((string)t.Attribute("name")),IsFavorite=(string)t.Attribute("favorite")=="true"};
     Coordinate a,b;double range;
     if(saved.Origin.Length>512||saved.Target.Length>512||saved.Distance.Length>12||saved.Summary.Length>1200)continue;
     if(!Calculator.TryCoordinate(saved.Origin,out a)||!Calculator.TryCoordinate(saved.Target,out b))continue;
     if(!String.IsNullOrWhiteSpace(saved.Distance)&&(!Calculator.TryNumber(saved.Distance.Trim(),out range)||range<=0||range>25000))continue;
     Saved.Add(saved);
    }
    RefreshHistory();
   }catch(Exception ex){if(!(ex is IOException)&&!(ex is InvalidDataException)&&!(ex is UnauthorizedAccessException)&&!(ex is XmlException))throw;Saved.Clear();SetInputs("","","");RefreshHistory();Set("Footer","Previous session could not be read. Start with fresh coordinates.");}
  }
  void ShowHelp(){
   var help=new Window{Title="How to use • Mortar Fast Calc",Owner=Window,Width=670,Height=660,MinWidth=480,MinHeight=400,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Color("#192127"),Foreground=Color("#EDF1F2"),FontFamily=new FontFamily("Segoe UI"),FontSize=14};
   var panel=new StackPanel{Margin=new Thickness(25)};
   panel.Children.Add(new TextBlock{Text="From map to mortar",FontSize=25,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)});
   string text="1. On the WARDOGS map, right-click your mortar and choose Mark Coordinates. Copy the X/Y pair from the chat input. Paste it into Your mortar. Repeat for your target.\n\n2. Leave Distance blank for automatic range. If the game gives you a measured distance, enter it in meters; the bearing still comes from the coordinates.\n\n3. In the mortar sight, turn to the displayed compass heading and adjust the left-side RNG readout to the displayed meters. Check direction against your target ping. The community MIL estimate is a secondary guide.\n\nKEYBOARD\nTab / Shift+Tab — move between controls\nCtrl+1 / Ctrl+2 / Ctrl+3 — select mortar / target / distance\nEnter in an input — save the current setup\nCtrl+N — clear target and distance, keep your mortar\nCtrl+Shift+C — copy the callout\nCtrl+H — saved targets; arrows then Enter to restore\nDelete in saved targets — remove the selected setup\nCtrl+T — toggle keep on top   ·   F1 — this guide\nAlt+F4 — close the app\n\nCOORDINATES & DATA\nSupported game maps: Bakurani, Ozeti, Zestafona. All three use 100 meters per coordinate unit in the researched community map configuration. X increases east; Y increases north. Compass: 0° N, 90° E, 180° S, 270° W.\n\nAccepted: x98.43, y110.38 · 98.43 110.38 · (98.43, 110.38). Labeled decimal commas also work: x98,43 y110,38. Use decimal points for unlabeled pairs.\n\nThe L81 table covers 132–684 m. MIL values are linearly interpolated from community game measurements, researched 19 September 2026. No terrain-height or obstruction correction is applied. Game patches can change the values; the current in-game HUD is authoritative.\n\nThis is an offline, unofficial game companion. It does not read the game, automate input, or send network traffic. Settings and up to 20 saved setups live in %LOCALAPPDATA%\\WardogsFastCalc\\session.xml.\n\nSOURCE\nApollyon’s open-source wardogs-calculator (MIT), with its data snapshot and license included in this delivery. See RESEARCH.md for exact links and commit.\nhttps://github.com/apollyon-sys/wardogs-calculator";
   text=text.Replace("Ctrl+H — saved targets; arrows then Enter to restore","Ctrl+H — saved targets; arrows select, Enter uses target\nShift+Enter in saved targets — restore the complete setup\nCtrl+5 — target name; Ctrl+F — search saved targets\nF2 / F in saved targets — rename / toggle favorite");
   text += "\n\nSAVED TARGETS & WINDOW\nGive a target an optional name before saving. Use target (Enter or double-click) retains your current mortar position, clears any distance override, and recalculates. Restore setup (Shift+Enter) restores the saved mortar position and override too. Hover a row for the complete saved setup.\n\nSearch matches names and coordinates. Down or Enter in search focuses the results; Esc clears the filter. Favorites appear first and are protected when the 20-target limit is reached.\n\nThe aiming values stay visible while the controls scroll. Narrow windows stack the panels; wider windows place them side by side. Window size, position, and maximized state are remembered. If a monitor is disconnected, the window is moved onto an available work area.";
   text += "\n\n3D ALIGNMENT VIEW\nCtrl+4 focuses the view. Arrow keys orbit; plus/minus zoom; Space toggles overhead; Home resets. You can also drag to orbit and scroll to zoom.\n\nBearing follows the calculated direction. Tube tilt illustrates relative MIL elevation; it is not a literal angle. The target ring is not to scale. Camera controls never change your aiming solution.";
   panel.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,LineHeight=21});
   var close=new Button{Content="Close · Esc",Padding=new Thickness(18,8,18,8),Margin=new Thickness(0,18,0,0),IsCancel=true};close.Click+=delegate{help.Close();};panel.Children.Add(close);
   help.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};help.ShowDialog();
  }
  public void RenderTo(string path){Window.UpdateLayout();var target=new RenderTargetBitmap((int)Math.Ceiling(Window.ActualWidth),(int)Math.Ceiling(Window.ActualHeight),96,96,PixelFormats.Pbgra32);target.Render(Window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(target));using(var stream=File.Create(path))png.Save(stream);}
 }
 public static class Program {
  [STAThread] public static int Main(string[] args){
   try{
    if(args.Length>0&&args[0]=="--test")return Tests.Run(args.Length>1?args[1]:"test-results.txt");
    var app=new Application();var controller=new Controller(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WardogsFastCalc","session.xml"));app.Run(controller.Window);return 0;
   }catch(Exception ex){MessageBox.Show("Mortar Fast Calc could not start.\n\n"+ex.Message,"Mortar Fast Calc",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
  }
 }
}
