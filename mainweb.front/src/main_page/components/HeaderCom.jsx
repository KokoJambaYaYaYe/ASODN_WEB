import React from 'react';
import { AppBar, Toolbar, Typography, Button } from '@mui/material';
import AccountCircleIcon from '@mui/icons-material/AccountCircle';

export default function HeaderCom({ activeModule, onNavigate }) {
    if (activeModule !== 'hub') return null;

    return (
        <AppBar position="static" className="app-bar">
            <Toolbar className="toolbar">
                <Typography variant="h6" component="div" className="title">
                    АСОДН
                </Typography>
                <Button
                    color="inherit"
                    startIcon={<AccountCircleIcon />}
                    onClick={() => onNavigate('user_data')}
                    className="profile-button"
                >
                    Профиль пользователя
                </Button>
            </Toolbar>
        </AppBar>
    );
}
