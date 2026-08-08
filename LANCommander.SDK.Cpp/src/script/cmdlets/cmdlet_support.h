#ifndef LANCOMMANDER_CMDLET_SUPPORT_H
#define LANCOMMANDER_CMDLET_SUPPORT_H

// picoposh's internal headers have no extern "C" guards — only the public
// picoposh.h does — so including them from C++ would give the declarations C++
// linkage and fail to resolve against the C-compiled library. Wrap them.
extern "C" {
#include "pico_array.h"
#include "pico_cmd.h"
#include "pico_cmd_util.h"
#include "pico_eval.h"
#include "pico_object.h"
#include "pico_param_bind.h"
#include "pico_pipeline.h"
#include "pico_str.h"
#include "pico_value.h"
}

#include <string>
#include <vector>

namespace lancommander {
namespace cmdlets {

// --- reading parameters ---------------------------------------------------
//
// Each returns false when the parameter was not supplied, leaving *out alone,
// so a caller can distinguish "absent" from "empty".

bool arg_string(PicoStage* ctx, const char* name, int position, std::string* out);
bool arg_long(PicoStage* ctx, const char* name, int position, long* out);
bool arg_double(PicoStage* ctx, const char* name, int position, double* out);

// A bool-valued parameter, as `-Utf16 1`. Note this is not a switch: the .NET
// cmdlets declare these as `bool`, which PowerShell also requires a value for.
bool arg_bool(PicoStage* ctx, const char* name, bool* out);

// A byte array, accepted either as an array of numbers (what
// ConvertTo-StringBytes emits) or as a string, whose characters are its bytes.
bool arg_bytes(PicoStage* ctx, const char* name, int position,
               std::vector<unsigned char>* out);

// --- emitting results -----------------------------------------------------

pico_status emit_string(PicoStage* ctx, const std::string& value);
pico_status emit_long(PicoStage* ctx, long value);
pico_status emit_bool(PicoStage* ctx, bool value);
pico_status emit_bytes(PicoStage* ctx, const std::vector<unsigned char>& bytes);

// Emits a JSON document as a picoposh value: objects become property bags,
// arrays become arrays, scalars become scalars. This is how the manifest
// cmdlets return an object with full fidelity without a second model layer.
pico_status emit_json(PicoStage* ctx, const std::string& json);

// --- converting the other way ---------------------------------------------

// A picoposh value as a compact JSON document. The inverse of emit_json, and
// the way anything object-shaped leaves a script: JSON is the SDK's internal
// interchange format, so this feeds the model parsers and the YAML emitter
// alike.
std::string value_to_json(pico_value v);

// Records a terminating error, phrased like picoposh's own cmdlets:
//   "<cmdlet>: <message>"
pico_status fail(PicoStage* ctx, pico_status status, const std::string& message);

// Sets a variable in the running interpreter. New-Package uses this to assign
// $Return the way its .NET counterpart does via SessionState.
bool set_variable(PicoStage* ctx, const char* name, const std::string& value);
bool set_variable_json(PicoStage* ctx, const char* name, const std::string& json);

} // namespace cmdlets
} // namespace lancommander

#endif // LANCOMMANDER_CMDLET_SUPPORT_H
