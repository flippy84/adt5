struct VS_IN
{
	float4 pos : POSITION;
	float2 tex : TEXCOORD0;
	float2 map : TEXCOORD1;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float2 tex : TEXCOORD0;
	float2 map : TEXCOORD1;
};

float4x4 worldViewProj;
Texture2D layer[4];
SamplerState pictureSampler;

PS_IN VS( VS_IN input )
{
	PS_IN output = (PS_IN)0;
	
	output.pos = mul(input.pos, worldViewProj);
	output.tex = input.tex;
	output.map = input.map;
	
	return output;
}

float4 PS( PS_IN input ) : SV_Target
{
	//return layer[0].Sample(pictureSampler, input.tex);
	float4 color[4];
	color[0] = layer[0].Sample(pictureSampler, input.map);
	color[1] = layer[1].Sample(pictureSampler, input.tex);
	color[2] = layer[2].Sample(pictureSampler, input.tex);
	color[3] = layer[3].Sample(pictureSampler, input.tex);

	return color[0];
}