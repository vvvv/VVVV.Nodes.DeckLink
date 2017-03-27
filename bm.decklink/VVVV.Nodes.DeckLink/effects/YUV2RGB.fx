
Texture2D InputTexture;

SamplerState pointSampler : IMMUTABLE
{
    Filter = MIN_MAG_MIP_POINT;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct psInput
{
	float4 p : SV_Position;
	float2 uv : TEXCOORD0;
};

int CompressedWidth = 1920/2;

float4 PS(psInput In): SV_Target
{
	uint width = CompressedWidth * 2;
	uint pixel = lerp(0, width, In.uv.x);
	bool rightPixel = pixel % 2 == 0;
	
    float4 uyvy = InputTexture.Sample(pointSampler, In.uv);
	float y1 = uyvy.a;
	float y2 = uyvy.g;
	float u = uyvy.b;
	float v = uyvy.r;
	
	float y = rightPixel ? y2 : y1;
	
	float c = y - (16.0f / 256.0f);
	float d = u - 0.5f;
	float e = v - 0.5;
	
	float4 col;
	col.r = 1.164383 * c + 1.596027 * e;
	col.g = 1.164383 * c - (0.391762 * d) - (0.812968 * e);
	col.b = 1.164383 * c +  2.017232 * d;
	col.a = 1.0f;
	
    return col.bgra;
}

technique11 Process
{
	pass P0 
	{
		SetPixelShader(CompileShader(ps_5_0,PS()));
	}
}
