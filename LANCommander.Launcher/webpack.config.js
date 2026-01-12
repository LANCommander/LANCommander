const path = require('path');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');

module.exports = {
    entry: ['./_Imports.razor.scss'], // Adjust the path if your index.js is located elsewhere
    module: {
        rules: [
            {
                test: /\.(css)$/,
                use: [
                    MiniCssExtractPlugin.loader,
                    {
                        loader: 'css-loader',
                        options: { sourceMap: true }
                    }
                ],
            },
            {
                test: /\.s[ac]ss$/i,
                use: [
                        MiniCssExtractPlugin.loader,
                        {
                            loader: 'css-loader',
                            options: { sourceMap: true }
                        },
                        {
                            loader: 'sass-loader',
                            options: { sourceMap: true }
                        }
                    ],
            }
        ],
    },
    output: {
        path: path.resolve(__dirname, 'wwwroot', 'css'), // The output directory
    },
    plugins: [
        new MiniCssExtractPlugin({
            filename: "app.css"
        })
    ],
    mode: 'production', // Use 'production' for minified output
    devtool: 'source-map'
};