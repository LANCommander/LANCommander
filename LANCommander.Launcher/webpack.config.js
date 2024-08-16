const path = require('path');

module.exports = {
    entry: './index.js', // Adjust the path if your index.js is located elsewhere
    output: {
        filename: 'bundle.js', // The output file
        path: path.resolve(__dirname, 'wwwroot'), // The output directory
    },
    mode: 'production', // Use 'production' for minified output
};