#ifndef BLEND_INC
#define BLEND_INC

fixed overlay(fixed a, fixed b)
{
    return a < 0.5 ? (2 * a * b) : (1 - 2 * (1 - a) * (1 - b));
}

fixed3 overlay(fixed3 a, fixed3 b)
{
    return fixed3(overlay(a.r, b.r), overlay(a.g, b.g), overlay(a.b, b.b));
}

fixed4 overlay(fixed4 a, fixed3 b)
{
    return fixed4(overlay(a.r, b.r), overlay(a.g, b.g), overlay(a.b, b.b), a.a);
}

#endif