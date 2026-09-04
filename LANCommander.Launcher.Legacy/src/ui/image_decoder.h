#ifndef LAUNCHER_UI_IMAGE_DECODER_H
#define LAUNCHER_UI_IMAGE_DECODER_H

// Raw decoded image data — backend-neutral by design, so the graphics layer
// and the decoder can change independently.
struct DecodedImage
{
    unsigned char *pixels; // Row-major, 4 bytes per pixel: R, G, B, A
    int width;
    int height;
};

// Call once at startup / shutdown.
void image_decoder_init();
void image_decoder_shutdown();

// Decode an image file (PNG, JPEG, BMP, GIF) and scale it to fit within
// max_w x max_h while preserving aspect ratio. Caller must call
// free_decoded_image() when done.
bool decode_image_file(const char *path, int max_w, int max_h, DecodedImage *out);

// Decode an image file and scale it so it COVERS min_w x min_h — the smallest
// aspect-preserving size where neither dimension falls short. One dimension
// overhangs; the caller crops it, typically with gfx::blit_region. This is
// CSS `object-fit: cover` to decode_image_file's `contain`.
bool decode_image_file_fill(const char *path, int min_w, int min_h, DecodedImage *out);

// Decode an image file and scale it to fit ENTIRELY within box_w x box_h,
// enlarging it if it is smaller than the box. This is what an Avalonia Viewbox
// does, and it is what a logo wants: decode_image_file() never upscales, so a
// small logo stayed small no matter how much room it was given.
bool decode_image_file_contain(const char *path, int box_w, int box_h, DecodedImage *out);

// Same as decode_image_file but decodes from a memory buffer instead of a file path.
bool decode_image_memory(const void *data, int data_size, int max_w, int max_h, DecodedImage *out);

// Decode an image shipped alongside the executable, named relative to the
// assets/ directory (e.g. "backgrounds/aoe2.jpg"). Replaces the old
// RT_RCDATA resource lookup, which was Win32-only.
bool decode_image_asset(const char *name, int max_w, int max_h, DecodedImage *out);

void free_decoded_image(DecodedImage *img);

#endif // LAUNCHER_UI_IMAGE_DECODER_H
