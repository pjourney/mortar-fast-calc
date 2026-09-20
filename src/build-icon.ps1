$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
# Native vector rendering keeps the ICO sharp at every Windows scale factor.
$markup = @'
<Viewbox xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="256" Height="256">
 <Canvas Width="256" Height="256">
  <Rectangle Canvas.Left="8" Canvas.Top="8" Width="240" Height="240" RadiusX="52" RadiusY="52" Stroke="#435263" StrokeThickness="2"><Rectangle.Fill><LinearGradientBrush StartPoint="0,0" EndPoint="0.8,1"><GradientStop Color="#202A36" Offset="0"/><GradientStop Color="#090E15" Offset="1"/></LinearGradientBrush></Rectangle.Fill></Rectangle>
  <Path Data="M49 157 A85 85 0 0 1 158 47" Stroke="#475D70" StrokeThickness="5" StrokeStartLineCap="Round" StrokeEndLineCap="Round"/>
  <Path Data="M43 113 H56 M75 52 L82 63 M120 35 V48" Stroke="#708699" StrokeThickness="5" StrokeStartLineCap="Round" StrokeEndLineCap="Round"/>
  <Path Data="M119 132 L174 188 M136 132 L158 189" Stroke="#D0DCE5" StrokeThickness="9" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/>
  <Path Data="M57 184 Q103 162 149 184 L158 194 Q104 212 48 194 Z" Fill="#8B9FAF"/>
  <Path Data="M57 184 Q103 172 149 184" Stroke="#D5E2EA" StrokeThickness="5" StrokeStartLineCap="Round" StrokeEndLineCap="Round"/>
  <Path Data="M96 164 L128 75 Q130 68 137 70 L157 77 Q163 79 160 87 L129 175 Q115 184 96 164 Z"><Path.Fill><LinearGradientBrush StartPoint="0,0" EndPoint="1,0.5"><GradientStop Color="#FAFDFF" Offset="0"/><GradientStop Color="#AAB9C8" Offset="1"/></LinearGradientBrush></Path.Fill></Path>
  <Path Data="M128 77 L157 87" Stroke="#607487" StrokeThickness="6"/>
  <Path Data="M107 138 L139 150" Stroke="#6E869A" StrokeThickness="10"/>
  <Path Data="M150 190 H180" Stroke="#94A8B9" StrokeThickness="9" StrokeStartLineCap="Round" StrokeEndLineCap="Round"/>
  <Ellipse Canvas.Left="180" Canvas.Top="46" Width="32" Height="32" Stroke="#9BE5F1" StrokeThickness="5"/>
  <Path Data="M196 37 V48 M196 76 V87 M171 62 H182 M210 62 H221" Stroke="#9BE5F1" StrokeThickness="5" StrokeStartLineCap="Round" StrokeEndLineCap="Round"/>
  <Ellipse Canvas.Left="192" Canvas.Top="58" Width="8" Height="8" Fill="#F0FDFF"/>
 </Canvas>
</Viewbox>
'@
$sizes = @(16,20,24,32,40,48,64,128,256)
$frames = [Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
 $view = [Windows.Markup.XamlReader]::Parse($markup)
 $view.Width = $size; $view.Height = $size
 $view.Measure([Windows.Size]::new($size,$size)); $view.Arrange([Windows.Rect]::new(0,0,$size,$size)); $view.UpdateLayout()
 $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $bitmap.Render($view)
 $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
 $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $stream = [IO.MemoryStream]::new(); $encoder.Save($stream); $frames.Add($stream.ToArray()); $stream.Dispose()
 if ($size -eq 256) { [IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'AppIcon.png'),$frames[$frames.Count-1]) }
}
$file = [IO.File]::Create((Join-Path $PSScriptRoot 'AppIcon.ico'))
$writer = [IO.BinaryWriter]::new($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16*$sizes.Count
for ($i=0; $i -lt $sizes.Count; $i++) {
 $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
 $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
 $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
 $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $writer.Write($frame) }
$writer.Dispose(); $file.Dispose()
Write-Output 'Generated AppIcon.ico with 9 sizes (16 through 256 pixels).'
