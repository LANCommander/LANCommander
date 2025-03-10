﻿const path = require('path');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');
const CopyWebpackPlugin = require('copy-webpack-plugin');

module.exports = {
    entry: ['./Main.ts', './Styles/ui.scss'], // Adjust the path if your index.js is located elsewhere
    module: {
        rules: [
            {
                test: /\.tsx?$/,
                use: 'ts-loader',
                exclude: /nodemodules/,
            },
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
    resolve: {
        extensions: ['.tsx', '.ts', '.js'],
    },
    output: {
        filename: 'bundle.js', // The output file
        path: path.resolve(__dirname, 'wwwroot'), // The output directory
    },
    plugins: [
        new MiniCssExtractPlugin({
            filename: "ui.css"
        }),
        new CopyWebpackPlugin({
            patterns: [
                {
                    from: path.resolve(__dirname, 'node_modules/bootstrap-icons/bootstrap-icons.svg'),
                    to: path.resolve(__dirname, 'wwwroot')
                }
            ]
        }),
    ],
    mode: 'production', // Use 'production' for minified output
    devtool: 'source-map'
};