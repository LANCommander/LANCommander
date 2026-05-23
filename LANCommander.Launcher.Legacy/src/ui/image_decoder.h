#ifndef LAUNCHER_UI_IMAGE_DECODER_H
#define LAUNCHER_UI_IMAGE_DECODER_H

// Raw decoded image data. No Allegro types here so this header can be
// included from files that use GDI+ (which conflicts with Allegro's BITMAP).
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

void free_decoded_image(DecodedImage *img);

#endif // LAUNCHER_UI_IMAGE_DECODER_H
