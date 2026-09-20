using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace FoxCursor.AssetTool;

// Minimal dependency-free codec for the included non-interlaced, 8-bit RGB/RGBA PNG.
// Not a general-purpose image library; validates dimensions/chunk CRC before decoding.
internal sealed class Png(int width, int height)
{
    internal int Width { get; } = width;
    internal int Height { get; } = height;
    internal byte[] Pixels { get; } = new byte[checked(width * height * 4)];
    internal static Png Read(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> signature = stackalloc byte[8]; stream.ReadExactly(signature);
        if (!signature.SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) throw new InvalidDataException("PNG signature missing.");
        using var compressed = new MemoryStream();
        int width=0,height=0,channels=0;
        byte[] number = new byte[4];
        while (true)
        {
            stream.ReadExactly(number); int length = BinaryPrimitives.ReadInt32BigEndian(number);
            if (length < 0 || length > 64*1024*1024) throw new InvalidDataException("Invalid PNG chunk size.");
            byte[] type = new byte[4]; stream.ReadExactly(type);
            byte[] data = new byte[length]; stream.ReadExactly(data); stream.ReadExactly(number);
            if (BinaryPrimitives.ReadUInt32BigEndian(number) != Crc(type,data)) throw new InvalidDataException("PNG CRC mismatch.");
            string name = Encoding.ASCII.GetString(type);
            if (name == "IHDR")
            {
                if(length!=13) throw new InvalidDataException("Invalid PNG header.");
                width=BinaryPrimitives.ReadInt32BigEndian(data); height=BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4));
                if(width is <1 or >4096 || height is <1 or >4096 || data[8]!=8 || data[9] is not (2 or 6) || data[10]!=0 || data[11]!=0 || data[12]!=0)
                    throw new InvalidDataException("AssetTool supports RGB/RGBA8, non-interlaced PNG up to 4096px only.");
                channels=data[9]==6?4:3;
            }
            else if(name=="IDAT") compressed.Write(data);
            else if(name=="IEND") break;
        }
        if(channels==0)throw new InvalidDataException("PNG header missing.");
        var image=new Png(width,height); int stride=checked(width*channels);
        byte[] row=new byte[stride], previous=new byte[stride];
        compressed.Position=0;
        using var zlib=new ZLibStream(compressed,CompressionMode.Decompress);
        for(int y=0;y<height;y++)
        {
            int filter=zlib.ReadByte(); if(filter is <0 or >4)throw new InvalidDataException("Invalid PNG filter.");
            zlib.ReadExactly(row);
            for(int x=0;x<stride;x++)
            {
                int a=x>=channels?row[x-channels]:0,b=previous[x],c=x>=channels?previous[x-channels]:0;
                int prediction=filter switch {0=>0,1=>a,2=>b,3=>(a+b)/2,4=>Paeth(a,b,c),_=>0};
                row[x]=unchecked((byte)(row[x]+prediction));
            }
            for(int x=0;x<width;x++)
            {
                int target=(y*width+x)*4,source=x*channels;
                image.Pixels[target]=row[source];image.Pixels[target+1]=row[source+1];image.Pixels[target+2]=row[source+2];
                image.Pixels[target+3]=channels==4?row[source+3]:(byte)255;
            }
            (row,previous)=(previous,row);
        }
        return image;
    }
    internal void Save(string path) => File.WriteAllBytes(path,Encode());
    internal byte[] Encode()
    {
        using var output=new MemoryStream();output.Write(new byte[]{137,80,78,71,13,10,26,10});
        byte[] header=new byte[13];BinaryPrimitives.WriteInt32BigEndian(header,Width);BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),Height);header[8]=8;header[9]=6;
        Chunk(output,"IHDR",header);
        using var compressed=new MemoryStream();
        using(var zlib=new ZLibStream(compressed,CompressionLevel.SmallestSize,true))
            for(int y=0;y<Height;y++){zlib.WriteByte(0);zlib.Write(Pixels,y*Width*4,Width*4);}
        Chunk(output,"IDAT",compressed.ToArray());Chunk(output,"IEND",[]);return output.ToArray();
    }
    internal Png Crop(int left,int top,int width,int height)
    {
        var result=new Png(width,height);
        for(int y=0;y<height;y++)Array.Copy(Pixels,((top+y)*Width+left)*4,result.Pixels,y*width*4,width*4);
        return result;
    }
    internal Png Copy(){var result=new Png(Width,Height);Pixels.CopyTo(result.Pixels,0);return result;}
    internal Png Resize(int width,int height)
    {
        var result=new Png(width,height);
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            // Area average in premultiplied-alpha space for small tray icons.
            int x0=x*Width/width,x1=Math.Max(x0+1,(x+1)*Width/width),y0=y*Height/height,y1=Math.Max(y0+1,(y+1)*Height/height);
            long r=0,g=0,b=0,a=0;int count=0;
            for(int iy=y0;iy<y1;iy++)for(int ix=x0;ix<x1;ix++)
            {int at=(iy*Width+ix)*4;int alpha=Pixels[at+3];a+=alpha;r+=Pixels[at]*alpha;g+=Pixels[at+1]*alpha;b+=Pixels[at+2]*alpha;count++;}
            int target=(y*width+x)*4;
            if(a>0){result.Pixels[target]=(byte)(r/a);result.Pixels[target+1]=(byte)(g/a);result.Pixels[target+2]=(byte)(b/a);}
            result.Pixels[target+3]=(byte)(a/count);
        }
        return result;
    }
    private static int Paeth(int a,int b,int c){int p=a+b-c,pa=Math.Abs(p-a),pb=Math.Abs(p-b),pc=Math.Abs(p-c);return pa<=pb&&pa<=pc?a:pb<=pc?b:c;}
    private static void Chunk(Stream output,string name,byte[] data)
    {
        byte[] type=Encoding.ASCII.GetBytes(name);Span<byte> number=stackalloc byte[4];BinaryPrimitives.WriteInt32BigEndian(number,data.Length);output.Write(number);output.Write(type);output.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number,Crc(type,data));output.Write(number);
    }
    private static uint Crc(byte[] type,byte[] data)
    {
        uint crc=uint.MaxValue;
        foreach(byte b in type.Concat(data)){crc^=b;for(int bit=0;bit<8;bit++)crc=(crc&1)!=0?0xEDB88320^(crc>>1):crc>>1;}
        return crc^uint.MaxValue;
    }
}
