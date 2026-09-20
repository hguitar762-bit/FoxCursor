using System.Text.Json;
using FoxCursor.AssetTool;

string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourcePath=Path.Combine(root,"assets/source/fox-original.png");
if(!File.Exists(sourcePath)){Console.Error.WriteLine("Run from FoxCursor/ or pass its absolute directory as the first argument.");return 1;}
var source=Png.Read(sourcePath);
// Coordinates belong to the supplied 1254px reference, never an arbitrary image.
if(source.Width!=1254 || source.Height!=1254)throw new InvalidDataException("The reference must be the supplied 1254 × 1254 image.");
var working=source.Crop(25,35,source.Width-50,865);
int w=working.Width,h=working.Height;
bool[] exterior=new bool[w*h];var queue=new Queue<int>();
void Visit(int x,int y)
{
    int i=y*w+x;if(exterior[i])return;int at=i*4;
    int min=Math.Min(working.Pixels[at],Math.Min(working.Pixels[at+1],working.Pixels[at+2]));
    int max=Math.Max(working.Pixels[at],Math.Max(working.Pixels[at+1],working.Pixels[at+2]));
    if(min>=230&&max-min<18){exterior[i]=true;queue.Enqueue(i);}
}
for(int x=0;x<w;x++){Visit(x,0);Visit(x,h-1);}for(int y=0;y<h;y++){Visit(0,y);Visit(w-1,y);}
while(queue.TryDequeue(out int i))
{int x=i%w,y=i/w;if(x>0)Visit(x-1,y);if(x+1<w)Visit(x+1,y);if(y>0)Visit(x,y-1);if(y+1<h)Visit(x,y+1);}
int left=w,top=h,right=0,bottom=0;
for(int y=0;y<h;y++)for(int x=0;x<w;x++)
{
    int i=y*w+x,at=i*4;
    if(exterior[i]){working.Pixels[at+3]=0;continue;}
    left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
    int r=working.Pixels[at],g=working.Pixels[at+1],b=working.Pixels[at+2];
    if(x>=2&&y>=2&&x<w-2&&y<h-2&&Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))<=18&&
        (exterior[i-1]||exterior[i+1]||exterior[i-w]||exterior[i+w]||exterior[i-2]||exterior[i+2]||exterior[i-2*w]||exterior[i+2*w]))
    {
        // Remove neutral JPEG white matte only along the outside black outline.
        working.Pixels[at]=working.Pixels[at+1]=working.Pixels[at+2]=8;
        working.Pixels[at+3]=(byte)Math.Round(255*Math.Clamp((255-(r+g+b)/3d)/247,0,1));
    }
}
const int pad=10;
var sprite=new Png(right-left+1+pad*2,bottom-top+1+pad*2);
for(int y=top;y<=bottom;y++)Array.Copy(working.Pixels,(y*w+left)*4,sprite.Pixels,((y-top+pad)*sprite.Width+pad)*4,(right-left+1)*4);
int originX=25+left-pad,originY=35+top-pad;
(int X,int Y)[] split=[(763,342),(778,410),(780,475),(774,535),(755,596),(725,642),(702,672)];
bool IsBody(int x,int y)
{
    double boundary=split[0].X;
    if(y>=split[^1].Y)boundary=split[^1].X;
    else for(int i=0;i<split.Length-1;i++)if(y>=split[i].Y&&y<split[i+1].Y)
    {boundary=split[i].X+(split[i+1].X-split[i].X)*(y-split[i].Y)/(double)(split[i+1].Y-split[i].Y);break;}
    return x<=boundary;
}
var body=sprite.Copy();var tail=sprite.Copy();
for(int y=0;y<sprite.Height;y++)for(int x=0;x<sprite.Width;x++)
{
    int alpha=(y*sprite.Width+x)*4+3;
    if(!IsBody(x+originX,y+originY))body.Pixels[alpha]=0;
    // Five pixels of overlap stay behind the opaque body.
    if(IsBody(x+originX+5,y+originY))tail.Pixels[alpha]=0;
}
string output=Path.Combine(root,"src/FoxCursor/Assets");Directory.CreateDirectory(output);
sprite.Save(Path.Combine(output,"fox.png"));body.Save(Path.Combine(output,"fox-body.png"));tail.Save(Path.Combine(output,"fox-tail.png"));
// ICO entries carry PNG payloads (supported by Windows 10/11), centred without stretching.
int[] sizes=[16,24,32,48,64,128,256];var entries=new List<byte[]>();
foreach(int size in sizes)
{
    int rh=Math.Max(1,size*sprite.Height/sprite.Width);var resized=sprite.Resize(size,rh);var square=new Png(size,size);
    Array.Copy(resized.Pixels,0,square.Pixels,((size-rh)/2)*size*4,resized.Pixels.Length);entries.Add(square.Encode());
}
using(var writer=new BinaryWriter(File.Create(Path.Combine(output,"fox.ico"))))
{
    writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
    for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(entries[i].Length);writer.Write(offset);offset+=entries[i].Length;}
    foreach(byte[] entry in entries)writer.Write(entry);
}
int sample=(300*source.Width+385)*4;
var metadata=new {source="assets/source/fox-original.png",sourceCropOrigin=new[]{originX,originY},size=new[]{sprite.Width,sprite.Height},creamRGB=source.Pixels.Skip(sample).Take(3).Select(value => (int)value).ToArray(),leafPivot=new[]{(770-originX)/(double)sprite.Width*100,(485-originY)/(double)sprite.Height*70},method="C# exterior flood fill, preserved opaque belly, original interior RGB, neutral JPEG edge matte removal, original-junction layer masks. No Python and no character regeneration."};
File.WriteAllText(Path.Combine(output,"asset-metadata.json"),JsonSerializer.Serialize(metadata,new JsonSerializerOptions{WriteIndented=true})+"\n");
var preview=sprite.Copy();for(int i=0;i<preview.Pixels.Length;i+=4){int alpha=preview.Pixels[i+3];preview.Pixels[i]=(byte)((preview.Pixels[i]*alpha+48*(255-alpha))/255);preview.Pixels[i+1]=(byte)((preview.Pixels[i+1]*alpha+54*(255-alpha))/255);preview.Pixels[i+2]=(byte)((preview.Pixels[i+2]*alpha+49*(255-alpha))/255);preview.Pixels[i+3]=255;}
Directory.CreateDirectory(Path.Combine(root,"docs"));preview.Resize(840,840*preview.Height/preview.Width).Save(Path.Combine(root,"docs/character-preview.png"));
// Meaningful asset invariants: opaque white belly, transparent exterior, lossless interior.
if(sprite.Pixels[3]!=0||sprite.Pixels[((620-originY)*sprite.Width+475-originX)*4+3]!=255)throw new InvalidDataException("Transparency invariant failed.");
int original=(300*source.Width+385)*4,extracted=((300-originY)*sprite.Width+385-originX)*4;
if(!sprite.Pixels.AsSpan(extracted,4).SequenceEqual(source.Pixels.AsSpan(original,4)))throw new InvalidDataException("Interior pixels changed.");
var decoded=Png.Read(Path.Combine(output,"fox.png"));if(!decoded.Pixels.SequenceEqual(sprite.Pixels))throw new InvalidDataException("PNG roundtrip failed.");
Console.WriteLine($"Generated PNG/body/tail/ICO: {sprite.Width} × {sprite.Height}; transparency, interior pixels and PNG roundtrip verified.");
return 0;
