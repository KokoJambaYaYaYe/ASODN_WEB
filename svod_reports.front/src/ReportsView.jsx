import React from 'react';
import { Box, Typography } from '@mui/material';

export default function ReportsView({ reportId }) {
    const VITE_BACKEND_API_URL = import.meta.env.VITE_BACKEND_API_URL;

    if (!reportId) {
        return (
            <Box sx={{ p: 4, textAlign: 'center', border: '1px dashed #cbd5e1', borderRadius: '12px', mt: 2 }}>
                <Typography variant="body1" color="text.secondary">
                    Выберите отчет для просмотра.
                </Typography>
            </Box>
        );
    }

    return (
        <Box sx={{ width: '100%', height: 'calc(100vh - 64px)' }}>
            {/* 64px — это примерная высота AppBar шапки */}
            <iframe
                title={`FastReport-PDF-Container-${reportId}`}
                src={`${VITE_BACKEND_API_URL}/get-pdf-report?id=${reportId}`}
                style={{
                    width: '100%',
                    height: '100%',
                    border: 'none'
                }}
            />
        </Box>
    );
}
