#:property TargetFramework=net10.0
#:property ManagePackageVersionsCentrally=false
#:property ImplicitUsings=enable
#:property Nullable=enable

using System.Buffers.Binary;

var path = @"d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\map\layers\ndh-dtm-test.tif";
var bytes = File.ReadAllBytes(path);
if (bytes[0] != (byte)'I' || bytes[1] != (byte)'I') throw new Exception("not LE TIFF");
var ifd = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan((int)ifd));
Console.WriteLine($"IFD @ {ifd}, entries={count}");
uint width=0,height=0,bps=0,comp=0,spp=0,sampleFormat=0,stripOff=0,stripByte=0,rowsPerStrip=0;
for (var i=0;i<count;i++)
{
  var o = (int)ifd + 2 + i*12;
  var tag = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(o));
  var type = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(o+2));
  var n = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o+4));
  var val = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(o+8));
  object shown = val;
  if (type==3 && n==1) shown = (ushort)(val & 0xFFFF);
  Console.WriteLine($"tag={tag} type={type} n={n} val={shown}");
  switch(tag){
    case 256: width=val; break;
    case 257: height=val; break;
    case 258: bps = type==3 ? (val & 0xFFFF) : val; break;
    case 259: comp = type==3 ? (val & 0xFFFF) : val; break;
    case 277: spp = type==3 ? (val & 0xFFFF) : val; break;
    case 273: stripOff=val; break;
    case 279: stripByte=val; break;
    case 278: rowsPerStrip=val; break;
    case 339: sampleFormat = type==3 ? (val & 0xFFFF) : val; break;
  }
}
Console.WriteLine($"w={width} h={height} bps={bps} comp={comp} spp={spp} sf={sampleFormat} off={stripOff} bytes={stripByte}");
var floats = new float[width*height];
Buffer.BlockCopy(bytes, (int)stripOff, floats, 0, (int)(width*height*4));
float min=float.MaxValue,max=float.MinValue; int finite=0, nan=0;
foreach(var f in floats){
  if (float.IsNaN(f) || float.IsInfinity(f)) { nan++; continue; }
  finite++; if (f<min) min=f; if (f>max) max=f;
}
Console.WriteLine($"finite={finite} nan={nan} min={min:F2} max={max:F2}");
// sample center and corners
void P(int x,int y){ var v=floats[y*width+x]; Console.WriteLine($"({x},{y})={v:F2}"); }
P(0,0); P((int)width/2,(int)height/2); P((int)width-1,(int)height-1); P(100,100);
