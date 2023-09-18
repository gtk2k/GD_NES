shader_type spatial;
uniform sampler2D _PatternTex;
uniform int _Sprite_B2;
uniform float _SpriteTable;
uniform int _Pattern;
uniform vec4 _Sprite_Palette[16];

void fragment() {
	int hFlip = _Sprite_B2 & 0x40;
	int vFlip = _Sprite_B2 & 0x80;
	vec2 uv = vec2(UV.x, UV.y);
	float x = uv.x;
	float y = uv.y;
	float rx = 1.0 / 32.0;
	float ry = 1.0 / 16.0;
	float px = float(_Pattern & 0xf);
	float py = float(_Pattern >> 4);
	float ox = hFlip != 0 ? (1.0 - x) * rx : x * rx;
	float oy = vFlip != 0 ? (1.0 - y) * ry : y * ry;
	uv.x = (0.5 * _SpriteTable) + (px * rx) + ox;
	uv.y = (py * ry) + oy;
	int palIdx = 0;

	vec4 col = texture(_PatternTex, uv);
	if (col.r < 0.5 && col.g < 0.5 && col.b < 0.5) {
		palIdx = 0;
	}
	else if (col.r > 0.5) {
		palIdx = 3;
	}
	else if (col.g > 0.5) {
		palIdx = 2;
	}
	else {
		palIdx = 1;
	}
	if (palIdx == 0) {
		ALPHA = 0.0;
		ALBEDO = vec3(0, 0, 0);
		//COLOR = vec4(0, 0, 0, 0);
	}
	else
	{
		palIdx += (_Sprite_B2 & 3) << 2;
		ALPHA = 1.0;
		ALBEDO = _Sprite_Palette[palIdx].xyz; // col;
	}
}
