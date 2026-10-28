#ifndef LIGHTHOUSE_BREAKER_WAVE_PROFILE_INCLUDED
#define LIGHTHOUSE_BREAKER_WAVE_PROFILE_INCLUDED
#define BREAKER_CAPACITY 32
#define BREAKER_COLUMNS 32
#define BREAKER_ROWS 20
#define BREAKER_APPROACH_START 5.0
#define BREAKER_CURL_BACK 0.6
#define BREAKER_TAIL 2.2
#define BREAKER_CREST_SPLIT 0.7
#define BREAKER_MAX_CURL 2.4
#define BREAKER_MAX_SPREAD 3.5
#define BREAKER_MAX_SHEET 3.0
#define BREAKER_MIN_HEIGHT 0.6
#define BREAKER_MAX_HEIGHT 1.2

float4 _BreakerA[BREAKER_CAPACITY];
float4 _BreakerB[BREAKER_CAPACITY];
float4 _BreakerTimes;
float4 _BreakerSpilling;
float4 _BreakerPlunging;
float _BreakerSea;
float _BreakerCount;

struct BreakerState
{
    int phase;
    float progress;
    float4 shape;
    float height;
    float crestBack;
    float curlRamp;
    float spread;
    float sheetTop;
    float ribbonAlpha;
    float floorAlpha;
    float sheetAlpha;
};

float BreakerEase(float t)
{
    t = saturate(t);
    return t * t * (3.0 - 2.0 * t);
}

int BreakerPhaseAt(float age, out float progress)
{
    progress = 0;
    if (age < 0 || age >= _BreakerTimes.w) return 0;
    if (age < _BreakerTimes.x) { progress = age / _BreakerTimes.x; return 1; }
    if (age < _BreakerTimes.y) { progress = (age - _BreakerTimes.x) / (_BreakerTimes.y - _BreakerTimes.x); return 2; }
    if (age < _BreakerTimes.z) { progress = (age - _BreakerTimes.y) / (_BreakerTimes.z - _BreakerTimes.y); return 3; }
    progress = (age - _BreakerTimes.z) / (_BreakerTimes.w - _BreakerTimes.z);
    return 4;
}

float4 BreakerShapeFor(float steepness)
{
    return lerp(_BreakerSpilling, _BreakerPlunging, saturate(steepness));
}

BreakerState BreakerMakeState(float age, float strength, float rockHeight, float steepness)
{
    BreakerState st = (BreakerState)0;
    float p;
    st.phase = BreakerPhaseAt(age, p);
    st.progress = p;
    st.shape = BreakerShapeFor(steepness);
    float peak = lerp(BREAKER_MIN_HEIGHT, BREAKER_MAX_HEIGHT, strength);
    float spreadMax = BREAKER_MAX_SPREAD * lerp(0.6, 1.0, strength);
    float sheetMax = min(rockHeight * lerp(1.2, 1.8, strength) + 0.2, BREAKER_MAX_SHEET) * st.shape.z;
    if (st.phase == 1)
    {
        st.crestBack = lerp(BREAKER_APPROACH_START, BREAKER_CURL_BACK, BreakerEase(p));
        st.height = peak * lerp(0.5, 1.0, BreakerEase(p));
        st.curlRamp = lerp(0.2, 0.5, p);
        st.ribbonAlpha = saturate(p * 3.0);
    }
    else if (st.phase == 2)
    {
        st.crestBack = lerp(BREAKER_CURL_BACK, 0.2, p);
        st.height = peak;
        st.curlRamp = lerp(0.5, 1.0, p);
        st.ribbonAlpha = 1.0;
    }
    else if (st.phase == 3)
    {
        st.crestBack = lerp(0.2, 0.0, p);
        st.height = peak * (1.0 - BreakerEase(p));
        st.curlRamp = 1.0;
        st.ribbonAlpha = 1.0 - p;
        st.spread = lerp(0.5, spreadMax, BreakerEase(p));
        st.floorAlpha = p;
        st.sheetTop = sheetMax * BreakerEase(p);
        st.sheetAlpha = BreakerEase(p);
    }
    else if (st.phase == 4)
    {
        st.spread = spreadMax;
        st.floorAlpha = 1.0 - p * p;
        st.sheetTop = sheetMax;
        st.sheetAlpha = 1.0 - BreakerEase(p);
    }
    st.sheetAlpha *= step(0.01, st.shape.z);
    return st;
}

void BreakerSection(BreakerState st, float s, out float back, out float y)
{
    if (s < BREAKER_CREST_SPLIT)
    {
        float p = s / BREAKER_CREST_SPLIT;
        back = st.crestBack + BREAKER_TAIL * (1.0 - p);
        y = st.height * BreakerEase(p);
    }
    else
    {
        float q = (s - BREAKER_CREST_SPLIT) / (1.0 - BREAKER_CREST_SPLIT);
        float phi = q * BREAKER_MAX_CURL * st.shape.x * st.curlRamp;
        float lip = st.shape.y * st.height * 2.0;
        back = st.crestBack - lip * sin(phi);
        y = st.height - lip * (1.0 - cos(phi));
    }
}

float3 BreakerPoint(int part, BreakerState st, float2 impact, float2 f, float2 r, float width, float s, float c)
{
    float2 xz;
    float y;
    if (part == 0)
    {
        float back;
        BreakerSection(st, s, back, y);
        y *= sqrt(max(0.0, sin(c * 3.14159265)));
        xz = impact - f * back + r * ((c - 0.5) * width);
    }
    else if (part == 1)
    {
        xz = impact - f * (s * st.spread) + r * ((c - 0.5) * width * (1.0 + 0.4 * s));
        y = 0.04;
    }
    else
    {
        xz = impact - f * (0.06 + 0.35 * s * s) + r * ((c - 0.5) * width * (0.9 + 0.2 * s));
        y = s * st.sheetTop;
    }
    return float3(xz.x, y, xz.y);
}
#endif
